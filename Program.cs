using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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

        public delegate void IndexedBiOp<T1, T2>(T1 t1, T2 t2, int i1, int i2);

        public interface IVector<T> : IEnumerable<T>
        {
            int Length { get; }

            void AddComponent(T t, int optionalIndex);
            void Reset();
        }

        public class Tuple
        {
            public double V1;
            public double V2;

            public virtual double Dot(Tuple other)
                => V1 * other.V1 + V2 * other.V2;

            protected void Scale(double scale, Tuple result)
            {
                result.V1 = V1 * scale;
                result.V2 = V2 * scale;
            }

            protected void Add(Tuple other, Tuple res)
            {
                res.V1 = V1 + other.V1;
                res.V2 = V2 + other.V2;
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

            public static double operator *(Tuple a, Tuple b) => a.Dot(b);
        }

        // WTuple adapter
        class WTuple
        {
            public Tuple Content { get; }

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
            public Tuple Content { get; }

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

        public struct FuncStruct<T, TRes>
        {
            public Func<TRes, TRes, TRes> AddFunc;
            public Func<T, T, TRes> DotFunc;
            public Func<T, T, T> AddVecFunc;
            public Func<T, double, T> ScaleFunc;
        }

        public struct ScalarFuncStruct<T, T2, TRes>
        {
            public Func<T, T, T> AddFunc;
            public Func<T, T2, TRes> MultiplyFunc;
        }

        public static double DoubleAdd(double a, double b) => a + b;
        public static double DoubleMultiply(double a, double b) => a * b;

        public static double Dot(Tuple a, Tuple b) => a.Dot(b);
        public static Tuple Add(Tuple a, Tuple b) => a.Add(b);
        public static Tuple Scale(Tuple t, double s) => t.Scale(s);

        public static void Pair<T, T2>(IVector<T> a, IVector<T2> other, BiOp<T, T2> op)
        {
            var enum1 = a.GetEnumerator();
            var enum2 = other.GetEnumerator();
            var avail1 = enum1.MoveNext();
            var avail2 = enum2.MoveNext();
            for ( ; avail1 && avail2; avail1 = enum1.MoveNext(), avail2 = enum2.MoveNext())
            {
                var v1 = enum1.Current;
                var v2 = enum2.Current;
                op(v1, v2);
            }
        }

        public static void Discarte<T, T2>(IVector<T> a, IVector<T2> other, IndexedBiOp<T, T2> op)
        {
            var enum1 = a.GetEnumerator();
            var i = 0;
            for (var avail1 = enum1.MoveNext(); avail1; avail1 = enum1.MoveNext())
            {
                var v1 = enum1.Current;
                var j = 0;
                var enum2 = other.GetEnumerator();
                for (var avail2 = enum2.MoveNext(); avail2;
                    avail2 = enum2.MoveNext())
                {
                    var v2 = enum2.Current;
                    op(v1, v2, i, j);
                    j++;
                }
                i++;
            }
        }

        static FuncStruct<Tuple, double> TupleDoubleFuncs = new FuncStruct<Tuple, double>
        {
            AddFunc = DoubleAdd,
            AddVecFunc = Add,
            DotFunc = Dot,
            ScaleFunc = Scale
        };

        static ScalarFuncStruct<Tuple, double, Tuple> TupleDoubleScalarFuncs = new ScalarFuncStruct<Tuple, double, Tuple>
        {
            AddFunc = Add,
            MultiplyFunc = Scale
        };

        static FuncStruct<double, double> DoubleFuncs = new FuncStruct<double, double>
        {
            AddFunc = DoubleAdd,
            AddVecFunc = DoubleAdd,
            DotFunc = DoubleMultiply,
            ScaleFunc = DoubleMultiply
        };

        static ScalarFuncStruct<double, double, double> DoubleScalarFuncs 
            = new ScalarFuncStruct<double, double, double>
        {
            AddFunc = DoubleAdd,
            MultiplyFunc = DoubleMultiply
        };

        class PMatrix
        {
            public double[,] Data;

            public PMatrix(int rowNumber, int colNumber)
            {
                Data = new double[rowNumber, colNumber];
            }

            public PMatrix(int n) : this(n, n)
            {
            }

            public double this[int row, int col]
            {
                get => Data[row, col];
                set { Data[row, col] = value; }
            }

            public int RowNumber => Data.GetLength(0);
            public int ColNumber => Data.GetLength(1);

            public static PMatrix operator *(PMatrix a, PMatrix b)
                => a.Multiply(b);

            public static PMatrix operator +(PMatrix a, PMatrix b)
                => a.Add(b, true);

            public static PMatrix operator -(PMatrix a, PMatrix b)
                => a.Add(b, false);

            private PMatrix Add(PMatrix right, bool plus = false)
            {
                Debug.Assert(RowNumber == right.RowNumber);
                Debug.Assert(ColNumber == right.ColNumber);

                var result = new PMatrix(RowNumber, ColNumber);
                for (var i = 0; i < RowNumber; i++)
                {
                    for (var j = 0; j < ColNumber; j++)
                    {
                        var tmp = plus ? right[i, j] : -right[i, j];
                        result[i, j] = Data[i, j] + tmp;
                    }
                }
                return result;
            }

            public PMatrix Multiply(PMatrix right)
            {
                Debug.Assert(ColNumber == right.RowNumber);
                var result = new PMatrix(RowNumber, right.ColNumber);
                for (var i = 0; i < RowNumber; i++)
                {
                    for (var j = 0; j < right.ColNumber; j++)
                    {
                        double sum = 0;
                        for (var k = 0; k < ColNumber; k++)
                        {
                            sum += this[i, k] * right[k, j];
                        }
                        result[i, j] = sum;
                    }
                }
                return result;
            }

            public void RightMultiply<T>(IVector<T> left, IVector<T> result, 
                ScalarFuncStruct<T, double, T> funcs)
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
                            t = funcs.MultiplyFunc(leftv, s);
                        }
                        else
                        {
                            t = funcs.AddFunc(t, funcs.MultiplyFunc(leftv, s));
                        }
                        j++;
                    }
                    result.AddComponent(t, i);
                }
            }

            public void LeftMultiply<T>(IVector<T> right, IVector<T> result,
                ScalarFuncStruct<T, double, T> funcs)
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
                            t = funcs.MultiplyFunc(rightv, s);
                        }
                        else
                        {
                            t = funcs.AddFunc(t, funcs.MultiplyFunc(rightv, s));
                        }
                        j++;
                    }
                    result.AddComponent(t, i);
                }
            }

            public TRes Quadratic<T, TRes>(IVector<T> v, FuncStruct<T, TRes> funcs,
                ScalarFuncStruct<T, double, T> funcs2, Func<IVector<T>, IVector<T>, TRes> dot)
            {
                Debug.Assert(ColNumber == v.Length);
                Debug.Assert(RowNumber == v.Length);
                var tmp = new DottableVector<T, TRes>(v.Length, funcs);
                RightMultiply(v, tmp, funcs2);
                return dot(tmp, v);
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
                            Data[i, j] = v;
                        }
                        else
                        {
                            Data[i, j] = 0;
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
                        Data[i, j] *= v;
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

            protected Vector()
            {
            }

            public void CopyFrom(Vector<T> other)
            {
                Length = other.Length;
                Data.Clear();
                foreach (var d in other.Data)
                {
                    Data.AddLast(d);
                }
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

        class DottableVector<T, TRes> : Vector<T>
        {
            public FuncStruct<T, TRes> Funcs;

            public DottableVector(int len, FuncStruct<T, TRes> funcs)
                : base(len)
            {
                Funcs = funcs;
            }

            protected DottableVector() : base()
            {
            }

            public void CopyFrom(DottableVector<T, TRes> other)
            {
                base.CopyFrom(other);
                Funcs = other.Funcs;
            }

            #region IDottable members

            #endregion

            public static DottableVector<T, TRes> operator+(DottableVector<T, TRes> a, DottableVector<T, TRes> b)
                => a.Add(b);

            public static TRes operator *(DottableVector<T, TRes> a, DottableVector<T, TRes> b)
                => Dot(a, b, a.Funcs.AddFunc, a.Funcs.DotFunc);  

            public T First => Data.First != null ? Data.First.Value : default(T);

            public DottableVector<T, TRes> Add(DottableVector<T, TRes> t)
                => Add(t, Funcs.AddVecFunc);

            protected void Scale(double scale, IVector<T> res)
            {
                var i = 0;
                foreach (var v in Data)
                {
                    var nv = Funcs.ScaleFunc(v, scale);
                    res.AddComponent(nv, i);
                    i++;
                }
            }

            protected void Add<T2>(IVector<T2> t, IVector<T> res, Func<T, T2, T> add) 
            {
                var i = 0;

                var enum1 = GetEnumerator();
                var avail1 = enum1.MoveNext();

                var enum2 = t.GetEnumerator();
                var avail2 = enum2.MoveNext();
                for (; avail1 || avail2; avail1 = enum1.MoveNext(), avail2 = enum2.MoveNext())
                {
                    var v1 = avail1 ? enum1.Current : TypeFactory.Instance.Create<T>();
                    var v2 = avail2 ? enum2.Current : TypeFactory.Instance.Create<T2>();
                    var v = add(v1, v2);
                    res.AddComponent(v, i);
                    i++;
                }
            }

            public DottableVector<T, TRes> Scale(double scale)
            {
                var res = new DottableVector<T, TRes>(Length, Funcs);
                Scale(scale, res);
                return res;
            }

            public DottableVector<T, TRes> Add<T2>(IVector<T2> t, Func<T, T2, T> add)
            {
                var res = new DottableVector<T, TRes>(Length, Funcs);
                Add(t, res, add);
                return res;
            }
        }

        public static TRes Dot<T, T2, TRes>(IVector<T> x, IVector<T2> y, 
           Func<TRes, TRes, TRes> add, Func<T, T2, TRes> dot)
        {
            TRes result = default(TRes);
            bool first = true;
            Pair(x, y, (a, b) =>
            {
                var r = dot(a, b);
                if (first)
                {
                    result = r;
                    first = false;
                }
                else
                {
                    result = add(result, r);
                }
            });
            return result;
        }

        class DoubleDottableVector<T> : DottableVector<T, double>
        {
            public DoubleDottableVector(int len,
                Func<T, T, double> dot,
                Func<T, T, T> addvec,
                Func<T, double, T> scale) : this(len, 
                new FuncStruct<T, double>
                {
                    AddFunc = DoubleAdd,
                    DotFunc = dot,
                    AddVecFunc = addvec,
                    ScaleFunc = scale
                })
            {
            }

            public DoubleDottableVector(int len, FuncStruct<T, double> func)
                : base(len, func)
            {
            }

            protected DoubleDottableVector() : base()
            {
            }

            public static DoubleDottableVector<T> operator +(DoubleDottableVector<T> a, DoubleDottableVector<T> b)
                => a.Add(b, a.Funcs.AddVecFunc);

            public new DoubleDottableVector<T> Scale(double scale)
            {
                var res = new DoubleDottableVector<T>(Length, Funcs);
                Scale(scale, res);
                return res;
            }

            public new DoubleDottableVector<T> Add<T2>(IVector<T2> t, Func<T, T2, T> add)
            {
                var res = new DoubleDottableVector<T>(Length, Funcs);
                Add(t, res, add);
                return res;
            }

            public PMatrix DiscarteMultiply<T2>(IVector<T2> other, Func<T, T2, double> dot)
            {
                var result = new PMatrix(Length, other.Length);
                Discarte(this, other, (a, b, i, j) => result[i, j] = dot(a, b));
                return result;
            }

            public DoubleDottableVector<T> Clone()
            {
                var clone = new DoubleDottableVector<T>();
                clone.CopyFrom(this);
                return clone;
            }
        }

        class DoubleVector : DoubleDottableVector<double>
        {
            public DoubleVector(int len) : base(len, DoubleAdd, DoubleAdd, DoubleMultiply)
            {
            }

            protected DoubleVector() : base()
            {
            }

            public new DoubleVector Scale(double scale)
            {
                var res = new DoubleVector(Length);
                Scale(scale, res);
                return res;
            }

            public DoubleVector Add(IVector<double> t)
            {
                var res = new DoubleVector(Length);
                Add(t, res, DoubleAdd);
                return res;
            }
            
            public new DoubleVector Clone()
            {
                var clone = new DoubleVector();
                clone.CopyFrom(this);
                return clone;
            }
        }

        interface IRLSEMapper
        {
            void Reset();
            void Update(double x, double y);
            double MapXToY(double x);
            double MapYToX(double y);
        }

        // https://en.wikipedia.org/wiki/Recursive_least_squares_filter
        class RLSEMapper : IRLSEMapper
        {
            public DoubleDottableVector<Tuple> Ws;
            public DoubleDottableVector<Tuple> Xs;
            public int TapCount;
            public double Lambda;

            public double InvDelta { get; }

            public PMatrix P;

            /// <summary>
            ///  Construct the RLSE mapper
            /// </summary>
            /// <param name="tapCount">Filter order 'p' + 1</param>
            /// <param name="lambda">How much previous samples contribute to current, the greater the more</param>
            /// <param name="delta">Initial values on diagonal of P roughly corrspond to a priori auto-covariance of input</param>
            public RLSEMapper(int tapCount = 1, double lambda = 0.99, double delta = 1)
            {
                TapCount = tapCount;
                P = new PMatrix(TapCount);
                Ws = new DoubleDottableVector<Tuple>(TapCount, Dot, Add, Scale);
                Xs = new DoubleDottableVector<Tuple>(TapCount, Dot, Add, Scale);
                Lambda = lambda;
                InvDelta = 1 / delta;
                Reset();
            }

            public void Reset()
            {
                P.Indentity(InvDelta);
                Ws.Reset();
                Xs.Reset();
            }

            public void Update(double x, double y)
            {
                var sample = new Sample(new Tuple())
                {
                    X = x
                };
                Xs.AddFirst(sample.Content);

                var a = y - Xs * Ws;
                Console.WriteLine($"a={a}");

                var px = new DoubleDottableVector<Tuple>(P.RowNumber, TupleDoubleFuncs);
                P.LeftMultiply(Xs, px, TupleDoubleScalarFuncs);
                var xpx = P.Quadratic(Xs, TupleDoubleFuncs, TupleDoubleScalarFuncs,
                    (aa, bb) => Dot(aa, bb, DoubleAdd, Dot));
                var coeff = 1.0 / (Lambda + xpx);
                var g = px.Scale(coeff);

                var gx =  g.DiscarteMultiply(Xs, Dot);
                var gxp = gx * P;
                P -= gxp;
                P.ScaleBy(1.0 / Lambda);

                Ws = Ws + g.Scale(a);
            }

            public double MapXToY(double x)
            {
                var xs = Xs.Clone();
                xs.AddFirst(new Sample(new Tuple()) { X = x }.Content);
                return xs * Ws;
            }

            public double MapYToX(double y)
            {
                var xs = Xs.Clone();
                xs.AddFirst(new Sample(new Tuple()) { X = 0 }.Content);
                var y0 = xs * Ws;
                var d = y - y0;
                var wt = Ws.First;
                if (wt != null)
                {
                    var w = new WTuple(wt);
                    return d / w.K;
                }
                throw new InvalidOperationException("Model not established");
            }
        }

        class RLSEMapper2 : IRLSEMapper
        {
            public DoubleDottableVector<double> Ws;
            public DoubleDottableVector<double> Xs { get; }

            public double Lambda { get; }
            public double InvDelta { get; }

            public PMatrix P;

            public RLSEMapper2(double lambda = 0.99, double delta = 1)
            {
                P = new PMatrix(2);

                Ws = new DoubleDottableVector<double>(2, DoubleFuncs);
                Xs = new DoubleDottableVector<double>(2, DoubleFuncs);
                Lambda = lambda;
                InvDelta = 1 / delta;
                Reset();
            }

            public double MapXToY(double x)
            {
                var a = Ws.ToArray();
                return a[0] * x + a[1];
            }

            public double MapYToX(double y)
            {
                var a = Ws.ToArray();
                return (y - a[1]) / a[0];
            }

            public void Reset()
            {
                P.Indentity(InvDelta);
                Ws.Reset();
                Xs.Reset();
            }

            public void Update(double x, double y)
            {
                var sample = new Sample(new Tuple())
                {
                    X = x
                };
                Xs.AddFirst(1);
                Xs.AddFirst(x);

                var a = y - Xs * Ws;
                Console.WriteLine($"a={a}");

                var px = new DoubleDottableVector<double>(2, DoubleFuncs);
                P.LeftMultiply(Xs, px, DoubleScalarFuncs);
                var xpx = P.Quadratic(Xs, DoubleFuncs, DoubleScalarFuncs,
                    (aa, bb) => Dot(aa, bb, DoubleAdd, DoubleMultiply));
                var coeff = 1.0 / (Lambda + xpx);
                var g = px.Scale(coeff);

                var gx = g.DiscarteMultiply(Xs, DoubleMultiply);
                var gxp = gx * P;
                P -= gxp;
                P.ScaleBy(1.0 / Lambda);

                Ws = Ws + g.Scale(a);
            }
        }

        public static void Main(string[] args)
        {
            //var rlse = new RLSEMapper(1, 0.1, 400);
            var rlse = new RLSEMapper2(0.1, 400);
            var k = 7;
            var b = 3;
            var rand = new Random();
            var ampNoise = 0.6;
            for (var i = 0; i < 100; i++)
            {
                //var x = -5 + 10 * rand.NextDouble();
                var x = 20 * rand.NextDouble();
                var y = k * x + b + rand.NextDouble() * ampNoise;
                rlse.Update(x, y);
                var yn = rlse.MapXToY(x);
                //Console.WriteLine($"yn for x = {x} is {yn} actual y is {y}; w = {rlse.Ws.First.V1},{rlse.Ws.First.V2}");
                Console.WriteLine($"yn for x = {x} is {yn} actual y is {y}");
            }

            var testX = 4;
            var testY = rlse.MapXToY(testX);
            Console.WriteLine($"y for x = 4 is {testY}");

            var testY2 = 31;
            var testX2 = rlse.MapYToX(testY2);
            Console.WriteLine($"x for y = 31 is {testX2}");
        }
    }
}
