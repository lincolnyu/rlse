using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace ConsoleApplication
{
    public class Program
    {
        // Map between linearly releated pairs X and Y using Recursive Least Square Estimator
        // Suppose Y = k X + b
        //

        public class TypeFactory
        {
            public static readonly TypeFactory Instance = new TypeFactory();

            public T Create<T>()
            {
                if (typeof(T) == typeof(Tuple))
                {
                    return (T)(object)(new Tuple());
                }
                else if (typeof(T) == typeof(double))
                {
                    return (T)(object)0.0;
                }
                throw new NotSupportedException("Unsupported type");
            }
        }

        public delegate void BiOp<T1, T2>(T1 t1, T2 t2);

        public delegate void IndexedBiOp<T1,T2>(T1 t1, T2 t2, int i1, int i2);

        public static double DoubleAdd(double a, double b) => a+ b;

        interface IDottable<in T, TRes>
        {
            TRes Dot(T other);

            TRes Dot<T2>(T2 other) where T2 : IDottable<T2, TRes>;
        }

        interface ILinear<T>
        {
            T Scale(double scale);

            T Add<T2>(T2 t) where T2 : ILinear<T2>;
        }

        interface IVector<T> : IEnumerable<T>
        {
            int Length { get; }

            void AddComponent(T t, int optionalIndex);
            void Reset();
        }

        class Tuple : IDottable<Tuple, double>, ILinear<Tuple>
        {
            public double V1;
            public double V2;

            public virtual double Dot(Tuple other)
                => V1 * other.V1 + V2 * other.V2;

            public double Dot<T2>(T2 other) where T2 : IDottable<T2, double>
            {
                var t = other as Tuple;
                if (t != null)
                {
                    return Dot(t);
                }
                throw new ArgumentException("Dotting incompatible tuples");
            }

            protected void Scale(double scale, Tuple result)
            {
                result.V1 = V1 * scale;
                result.V2 = V2 * scale;
            }

            protected void Add<T>(T t, Tuple result) where T : ILinear<T>
            {
                var tt = t as Tuple;
                if (tt != null)
                {
                    result.V1 = V1 + tt.V1;
                    result.V2 = V2 + tt.V2;
                    return;
                }
                throw new ArgumentException("Adding incompatible tuples");
            }

            public virtual Tuple Scale(double scale)
            {
                var res = new Tuple();
                Scale(scale, res);
                return res;
            }

            public Tuple Add(Tuple t)
            {
                var res = new Tuple();
                Add(t, res);
                return res;
            }

            public Tuple Add<T>(T t) where T : ILinear<T>
            {
                var res = new Tuple();
                Add(t, res);
                return res;
            }

            public static double operator*(Tuple a, Tuple b) => a.Dot(b);
        }

        // WTuple adapter
        class WTuple
        {
            public Tuple Content {get;}

            public double K
            {
                get => Content.V1;
                set => Content.V1 = value;
            }

            public double B
            {
                get => Content.V2;
                set => Content.V2 = B;
            }

            public WTuple(Tuple tuple)
            {
                Content = tuple;
            }
        }

        class Sample
        {
            public Tuple Content {get;}

            public double X
            {
                get => Content.V1;
                set => Content.V1 = value;
            }

            public Sample(Tuple tuple)
            {
                Content = tuple;
                Content.V2 = 1;
            }
        }

        class PMatrix
        {
            public double[,] Data;
            
            public PMatrix(int rowNumber, int colNumber)
            {
                Data = new double[rowNumber, colNumber];
            }
            
            public PMatrix(int n) : this(n,n)
            {
            }

            public double this[int row, int col]
            {
                get => Data[row, col];
                set { Data[row,col] = value;}
            }

            public int RowNumber => Data.GetLength(0);
            public int ColNumber => Data.GetLength(1);
            
            public static PMatrix operator*(PMatrix a, PMatrix b)
                => a.Multiply(b);

            public static PMatrix operator+(PMatrix a, PMatrix b)
                => a.Add(b, true);

            public static PMatrix operator-(PMatrix a, PMatrix b)
                => a.Add(b, false);

            private PMatrix Add(PMatrix right, bool plus=false)
            {
                Debug.Assert(RowNumber == right.RowNumber);
                Debug.Assert(ColNumber == right.ColNumber);
                
                var result = new PMatrix(RowNumber, ColNumber);
                for (var i = 0; i <RowNumber; i++)
                {
                    for (var j = 0; j < ColNumber; j++)
                    {
                        var tmp = plus? right[i,j] : -right[i,j];
                        result[i,j] = Data[i,j] + tmp;
                    }
                }
                return result;
            }

            public PMatrix Multiply(PMatrix right)
            {
                Debug.Assert(ColNumber == right.RowNumber);
                var result = new PMatrix(RowNumber, right.ColNumber);
                for (var i = 0; i <RowNumber; i++)
                {
                    for (var j = 0; j < right.ColNumber; j++)
                    {
                        double sum = 0;
                        for (var k = 0; k < ColNumber; k++)
                        {
                            sum += this[i,k] * right[k,j];
                        }
                        result[i,j] = sum;
                    }
                }
                return result;
            }

            public void RightMultiply<T>(IVector<T> left, IVector<T> result)
                where T : ILinear<T>
            {
                Debug.Assert(RowNumber == left.Length);
                Debug.Assert(ColNumber == result.Length);
                for (var i = 0; i < ColNumber; i++)
                {
                    var j = 0;
                    var t = default(T);
                    foreach (var leftv in left)
                    {
                        var s = this[j, i];
                        if (j == 0)
                        {
                            t = leftv.Scale(s);
                        }
                        else
                        {
                            t = t.Add(leftv.Scale(s));
                        }
                        j++;
                    }
                    result.AddComponent(t, i);
                }
            }

            public void LeftMultiply<T>(IVector<T> right, IVector<T> result)
                  where T : ILinear<T>
            {
                Debug.Assert(ColNumber == right.Length);
                Debug.Assert(RowNumber == result.Length);
                for (var i = 0; i < RowNumber; i++)
                {
                    var j = 0;
                    var t = default(T);
                    foreach (var rightv in right)
                    {
                        var s = this[i, j];
                        if (j == 0)
                        {
                            t = rightv.Scale(s);
                        }
                        else
                        {
                            t = t.Add(rightv.Scale(s));
                        }
                        j++;
                    }
                    result.AddComponent(t, i);
                }
            }

            public TRes Quadratic<T, TRes>(IVector<T> v, Func<TRes,TRes,TRes> addRes) 
                where T : IDottable<T, TRes>, ILinear<T>
            {
                Debug.Assert(ColNumber == v.Length);
                Debug.Assert(RowNumber == v.Length);
                var tmp = new DottableVector<T, TRes>(v.Length, addRes);
                RightMultiply(v, tmp);
                return tmp.Dot(v);
            }

            public void Indentity(double v = 1.0)
            {
                Debug.Assert(RowNumber == ColNumber);
                for (var i = 0; i < RowNumber; i++)
                {
                    for (var j = 0; j < ColNumber; j++)
                    {
                        if (i == j)
                        {
                            Data[i,j] = v;
                        }
                        else
                        {
                            Data[i,j] = 0;
                        }
                    }
                }
            }

            public void ScaleBy(double v)
            {
                for (var i = 0; i < RowNumber; i++)
                {
                    for (var j = 0; j < ColNumber; j++)
                    {
                        Data[i,j] *= v;
                    }
                }
            }
        }

        class Vector<T> : IVector<T>
        {
            public int Length { get; private set; }
            public LinkedList<T> Data = new LinkedList<T>();
            
            public Vector(int len)
            {
                Length = len;
            }
            
            public void Fill(T t = default(T))
            {
                Data.Clear();
                for (var i = 0; i < Length; i++)
                {
                    Data.AddLast(t);
                }
            }
            public void AddFirst(T t)
            {
                Data.AddFirst(t);
                EnforceLength();
            }
            
            private void EnforceLength()
            {
                while (Data.Count > Length)
                {
                    Data.RemoveLast();
                }
            }
            
            public void Pair<T2>(IVector<T2> other, BiOp<T, T2> op)
            {
                var p1 = Data.First;
                var enum2 = other.GetEnumerator();
                for (var avail2 = enum2.MoveNext(); p1 != null && avail2; 
                    p1 = p1.Next, avail2 = enum2.MoveNext())
                {
                    var v2 = enum2.Current;
                    op(p1.Value, v2);
                }
            }

            public void Discarte<T2>(IVector<T2> other, IndexedBiOp<T, T2> op)
            {
                var i = 0;
                for (var p1 = Data.First; p1 != null; p1 = p1.Next)
                {
                    var j = 0;
                    var enum2 = other.GetEnumerator();
                    for (var avail2 = enum2.MoveNext(); avail2; 
                        avail2 = enum2.MoveNext())
                    {
                        var v2 = enum2.Current;
                        op(p1.Value, v2, i, j);
                        j++;
                    }
                    i++;
                }
            }

            public void AddComponent(T t, int optionalIndex)
            {
                Debug.Assert(optionalIndex == Data.Count);
                Data.AddLast(t);
            }

            public void Reset()
                => Data.Clear();

            public IEnumerator<T> GetEnumerator()
                => Data.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator()
                => GetEnumerator();
        }

        class DottableVector<T, TRes> : Vector<T>, IDottable<IVector<T>, TRes>, ILinear<DottableVector<T, TRes>>
            where T : IDottable<T, TRes>, ILinear<T>
        {
            public Func<TRes, TRes, TRes> AddFunc { get; }
            
            public DottableVector(int len, Func<TRes, TRes, TRes> add) : base(len)
            {
                AddFunc = add;
            }

            #region IDottable members

            #endregion

            public static DottableVector<T, TRes> operator+(DottableVector<T, TRes> a, DottableVector<T, TRes> b)
                => a.Add(b);

            public static TRes operator*(DottableVector<T, TRes> a, DottableVector<T, TRes> b)
                => a.Dot(b);

            public T First => Data.First != null? Data.First.Value : default(T);

            #region IDottable<IVector<T>, TRes> members

            public TRes Dot(IVector<T> other) => Dot<T>(other);
            public virtual TRes Dot<T2>(T2 other) where T2 : IDottable<T2, TRes>
            {
                throw new NotSupportedException();
            }

            #endregion

            #region ILinear<DottableVector<T, TRes>> members
            public virtual DottableVector<T, TRes> Add<T2>(T2 t) where T2 : ILinear<T2>
            {
                throw new NotSupportedException();
            }

            #endregion

            public DottableVector<T, TRes> Add(DottableVector<T, TRes> t)
                => Add((IVector<T>)t);


            protected void Scale(double scale, IVector<T> res)
            {
                var i = 0;
                foreach (var v in Data)
                {
                    var nv = v.Scale(scale);
                    res.AddComponent(nv, i);
                    i++;
                }
            }

            protected void Add<T2>(IVector<T2> t, IVector<T> res) where T2 : ILinear<T2>
            {
                var i = 0;

                var enum1 = GetEnumerator();
                var avail1 = enum1.MoveNext();

                var enum2 = t.GetEnumerator();
                var avail2 = enum2.MoveNext();
                for ( ; avail1 || avail2; avail1 = enum1.MoveNext(), avail2 = enum2.MoveNext())
                {
                    var v1 = avail1 ? enum1.Current : TypeFactory.Instance.Create<T>();
                    var v2 = avail2 ? enum2.Current : TypeFactory.Instance.Create<T2>();
                    var v = v1.Add<T2>(v2);
                    res.AddComponent(v, i);
                    i++;
                }
            }

            public DottableVector<T, TRes> Scale(double scale)
            {
                var res = new DottableVector<T, TRes>(Length, AddFunc);
                Scale(scale, res);
                return res;
            }

            public DottableVector<T, TRes> Add<T2>(IVector<T2> t) where T2 : ILinear<T2>
            {
                var res = new DottableVector<T, TRes>(Length, AddFunc);
                Add(t, res);
                return res;
            }

            public TRes Dot<T2>(IVector<T2> other) where T2 : IDottable<T2, TRes>
            {
                TRes result = default(TRes);
                bool first= true;
                Pair(other, (a,b)=> 
                    {
                        var r = a.Dot(b);
                        if (first) 
                        {
                            result = r;
                            first = false;
                        }
                        else
                        {
                            result = AddFunc(result, r);
                        }
                    }
                );
                return result;
            }
        }

        class DoubleDottableVector<T> : DottableVector<T, double> 
            where T : IDottable<T, double>, ILinear<T>
        {
            public DoubleDottableVector(int len) : base(len, DoubleAdd)
            {
            }

            public new DoubleDottableVector<T> Scale(double scale)
            {
                var res = new DoubleDottableVector<T>(Length);
                Scale(scale, res);
                return res;
            }

            public new DoubleDottableVector<T> Add<T2>(IVector<T2> t) where T2 : ILinear<T2>
            {
                var res = new DoubleDottableVector<T>(Length);
                Add(t, res);
                return res;
            }

            public PMatrix DiscarteMultiply<T2>(IVector<T2> other) where T2 : IDottable<T2, double>
            {
                var result = new PMatrix(Length, other.Length);
                Discarte(other, (a, b, i, j)=> result[i,j] = a.Dot(b));
                return result;
            }
        }

        // https://en.wikipedia.org/wiki/Recursive_least_squares_filter
        class RLSEMapper
        {
            public DoubleDottableVector<Tuple> Ws;
            public DoubleDottableVector<Tuple> Xs;
            public int TapCount;
            public double Lambda;

            public double InvDelta {get;}

            public PMatrix P;
            
            /// <summary>
            ///  Construct the RLSE mapper
            /// </summary>
            /// <param name="tapCount">Filter order 'p' + 1</param>
            /// <param name="lambda">How much previous samples contribute to current, the greater the more</param>
            /// <param name="delta">Initial values on diagonal of P roughly corrspond to a priori auto-covariance of input</param>
            public RLSEMapper(int tapCount = 5, double lambda = 0.9,  double delta = 1)
            {
                TapCount = tapCount;
                P = new PMatrix(TapCount);
                Ws = new DoubleDottableVector<Tuple>(TapCount);
                Xs = new DoubleDottableVector<Tuple>(TapCount);
                Lambda = lambda;
                InvDelta = 1/delta;
                Reset();
            }
            
            public void Reset()
            {
                P.Indentity(InvDelta);
                Ws.Reset();
                Xs.Reset();
            }
            
            public void RecordSample(double x, double y)
            {
                var sample = new Sample(new Tuple())
                {
                    X = x,
                };
                Xs.AddFirst(sample.Content);

                var a = y-Xs.Dot<Tuple>(Ws);

                var px = new DoubleDottableVector<Tuple>(P.RowNumber);
                P.LeftMultiply(Xs, px);
                var xpx = P.Quadratic<Tuple, double>(Xs, DoubleAdd);
                var coeff = 1.0/(Lambda+xpx);
                var g = px.Scale(coeff);

                var gx = g.DiscarteMultiply(Xs);
                var gxp = gx * P;
                P -= gxp;
                P.ScaleBy(1.0/Lambda);

                Ws = Ws.Add(g.Scale(a));
            }

            public double MapXToY(double x)
            {
                var wt = Ws.First;
                if (wt != null)
                {
                    var w = new WTuple(wt);
                    return w.K * x + w.B;
                }
                throw new InvalidOperationException("Model not established");
            }
            
            public double MapYToX(double y)
            {
                var wt = Ws.First;
                if (wt != null)
                {
                    var w = new WTuple(wt);
                    return (y - w.B) / w.K;
                }
                throw new InvalidOperationException("Model not established");
            }
        }

        public static void Main(string[] args)
        {
            var rlse = new RLSEMapper(1);
            var k = 3;
            var b = 2;
            var rand = new Random();
            var ampNoise = 0;// 0.6;
            for (var i = 0; i < 100; i++)
            {
                var x = -5 + 10 * rand.NextDouble();
                var y = k*x + b + rand.NextDouble() * ampNoise;
                rlse.RecordSample(x, y);
            }
        
            var testX = 4;
            var testY = rlse.MapXToY(testX);
            Console.WriteLine($"y for x = 4 is {testY}");
        }
    }
}
