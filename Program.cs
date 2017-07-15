using System;
using System.Collections.Generic;

namespace ConsoleApplication
{
    public class Program
    {
        // Map between linearly releated pairs X and Y using Recursive Least Square Estimator
        // Suppose Y = k X + b
        //

        public delegate void BiOp<T1, T2>(T1 t1, T2 t2);

        public delegate void IndexedBiOp<T1,T2>(T1 t1, T2 t2, int i1, int i2);

        interface IDottable<T, TRes>
        {
            TRes Dot(T other);
        }

        interface IScalable
        {
            void ScaleBy(double scale);
        }

        class Tuple : IDottable<Tuple, double>, IScalable
        {
            public double V1;
            public double V2;
            public double V3;

            public virtual double Dot(Tuple other)=> V1*other.V1+V2*other.V2 + V3*other.V3;

            public virtual void ScaleBy(double scale)
            {
                V1 *= scale;
                V2 *= scale;
                V3 *= scale;
            }

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
                System.Diagnostics.Debug.Assert(ColNumber == right.RowNumber);
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

            
            public Vector<T> RightMultiply<T>(Vector<T> left)
            {
                System.Diagnostics.Debug.Assert(RowNumber == left.Length);
                throw new NotImplementedException();
            }

            public Vector<T> LeftMultiply<T>(Vector<T> right)
            {
                System.Diagnostics.Debug.Assert(ColNumber == right.Length);
                throw new NotImplementedException();
            }


            public TRes Quadratic<T, TRes>(Vector<T> v) where T : IDottable<T, TRes>
            {
                var tmp = RightMultiply(v);
                var tmpDot = (IDottable<Vector<T>,TRes>)tmp;
                return tmpDot.Dot(v);
            }
        }

        class Vector<T>
        {
            public int Length;
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
            public void Add(T t)
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
            
            public void Pair<T2>(Vector<T2> other, BiOp<T, T2> op)
            {
                var p1 = Data.First;
                var p2 = other.Data.First;
                for ( ; p1 != null && p2 != null; p1 = p1.Next, p2 = p2.Next)
                {
                    op(p1.Value, p2.Value);
                }
            }

            public void Discarte<T2>(Vector<T2> other, IndexedBiOp<T, T2> op)
            {
                var i = 0;
                for (var p1 = Data.First; p1 != null; p1 = p1.Next)
                {
                    var j = 0;
                    for (var p2 = other.Data.First; p2 != null; p2 = p2.Next)
                    {
                        op(p1.Value, p2.Value, i, j);
                        j++;
                    }
                    i++;
                }
            }
        }

        class TupleVector : Vector<Tuple>, IDottable<Vector<Tuple>, double>
        {
            public TupleVector(int len) : base(len)
            {
            }

            public static double operator*(TupleVector a, TupleVector b)
                => a.Dot(b);

            public PMatrix DiscarteMultiply(TupleVector other)
            {
                var result = new PMatrix(Length, other.Length);
                Discarte(other, (a, b, i, j)=> result[i,j] = a*b);
                return result;
            }

            public double Dot(Vector<Tuple> other)
            {
                double result = 0;
                Pair(other, (a,b)=> result += a*b);
                return result;
            }
        }

        class RLSEMapper
        {
            public Vector<WTuple> Ws;
            public Vector<Sample> Xs;
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
                Xs.Add(new Sample(x,y));
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
