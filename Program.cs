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

        public delegate void BiOp<T1, T2>(T1 t1, T2 t2);

        public delegate void IndexedBiOp<T1,T2>(T1 t1, T2 t2, int i1, int i2);

        interface IDottable<in T, TRes>
        {
            TRes Dot(T other);
        }

        interface ILinear<T>
        {
            T Scale(double scale);
            T Add(T t);
        }

        class Tuple : IDottable<Tuple, double>, ILinear<Tuple>
        {
            public double V1;
            public double V2;
            public double V3;

            public virtual double Dot(Tuple other)
                => V1 * other.V1 + V2 * other.V2 + V3 * other.V3;

            public virtual Tuple Scale(double scale)
                => new Tuple
                {
                    V1 = V1 * scale,
                    V2 = V2 * scale,
                    V3 = V3 * scale
                };

            public Tuple Add(Tuple t)
                => new Tuple
                {
                    V1 = V1 + t.V1,
                    V2 = V2 + t.V2,
                    V3 = V3 + t.V3
                };

            public static double operator*(Tuple a, Tuple b) => a.Dot(b);
        }

        class WTuple : Tuple
        {
            public double K => V1;
            public double B => V3;
            public WTuple(double k, double b)
            {
                V1 = k;
                V2 = -1;
                V3 = b;
            }

            public override double Dot(Tuple other) => other.Dot(this);
            
        }

        class Sample : Tuple
        {
            public double X => V1;
            public double Y => V2;
            public Sample(double x, double y)
            {
                V1 = x;
                V2 = y;
            }

            public override double Dot(Tuple other)
            {
                if (other is WTuple w)
                {
                    return w.K * X + w.B - Y;
                }
                return base.Dot(other);
            }
        }

        interface IVector<T> : IEnumerable<T>
        {
            int Length { get; }

            void Add(T t, int optionalIndex);
            void Reset();
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
            
            public PMatrix Mutlply(PMatrix right)
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

            public static PMatrix operator*(PMatrix left, PMatrix right)
                => left.Mutlply(right);

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
                    result.Add(t, i);
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
                    result.Add(t, i);
                }
            }

            public TRes Quadratic<T, TRes>(IVector<T> v) where T : IDottable<T, TRes>,
                ILinear<T>
            {
                Debug.Assert(ColNumber == v.Length);
                Debug.Assert(RowNumber == v.Length);
                var tmp = new Vector<T>(v.Length);
                RightMultiply(v, tmp);
                var tmpDot = (IDottable<IVector<T>,TRes>)tmp;
                return tmpDot.Dot(v);
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

            public void Add(T t, int optionalIndex)
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

        class TupleVector<T> : Vector<T>, IDottable<IVector<T>, double> 
            where T : IDottable<T, double>
        {
            public TupleVector(int len) : base(len)
            {
            }

            public static double operator*(TupleVector<T> a, TupleVector<T> b)
                => a.Dot(b);

            public PMatrix DiscarteMultiply(TupleVector<T> other)
            {
                var result = new PMatrix(Length, other.Length);
                Discarte(other, (a, b, i, j)=> result[i,j] = a.Dot(b));
                return result;
            }

            public double Dot(IVector<T> other)
            {
                double result = 0;
                Pair(other, (a,b)=> result += a.Dot(b));
                return result;
            }
        }

        class RLSEMapper
        {
            public TupleVector<WTuple> Ws;
            public TupleVector<Sample> Xs;
            public int TapCount;
            public double Lambda;
            
            public RLSEMapper(double lambda = 0.99, int tapCount = 5)
            {
                Lambda = lambda;
                TapCount = tapCount;
                Reset();
            }
            
            public void Reset()
            {
                Ws.Fill();
            }
            
            public void RecordSample(double x, double y)
            {
                Xs.AddFirst(new Sample(x,y));
             //   Xs.Dot(Ws);
            }
        /*  
            public double MapXToY(double x)
            {
            }
            
            public double MapYToX(double Y)
            {
            }
            */
        }
        public static void Main(string[] args)
        {
            var s = new Sample(1,2);
            var w = new WTuple(3,4);
            var r = s*w;
            Console.WriteLine($"r={r}");
        }
    }
}
