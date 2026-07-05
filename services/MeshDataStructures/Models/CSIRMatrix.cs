using System;
using System.Collections.Generic;
using System.Linq;

namespace MeshDataStructures.Models
{
    public class CsirMatrix
    {
        private double[] _di;
        private double[] _altr;
        private double[] _autr;
        private int[] _jptr;
        private int[] _iptr;

        public int Size { get; private set; }
        public int NonZerosOffDiagonal => _jptr.Length;
        public int NonZerosTotal => Size + NonZerosOffDiagonal;

        public CsirMatrix(int n)
        {
            if (n <= 0)
                throw new ArgumentException("Размер матрицы должен быть положительным", nameof(n));
            Size = n;
            _di = new double[n];
            _altr = Array.Empty<double>();
            _autr = Array.Empty<double>();
            _jptr = Array.Empty<int>();
            _iptr = new int[n + 1];
        }

        public CsirMatrix(double[,] dense)
        {
            if (dense == null)
                throw new ArgumentNullException(nameof(dense));
            int n = dense.GetLength(0);
            if (n != dense.GetLength(1))
                throw new ArgumentException("Матрица должна быть квадратной", nameof(dense));
            Size = n;

            _di = new double[n];
            for (int i = 0; i < n; i++)
                _di[i] = dense[i, i];

            var lowerVals = new List<double>();
            var upperVals = new List<double>();
            var colIndices = new List<int>();
            var rowPtr = new List<int> { 0 };

            int count = 0;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    if (dense[i, j] != 0.0)
                    {
                        lowerVals.Add(dense[i, j]);
                        colIndices.Add(j);
                        count++;
                    }
                }
                rowPtr.Add(count);
            }

            var upperByCol = new List<double>();
            var rowIndices = new List<int>();
            for (int j = 0; j < n; j++)
            {
                for (int i = j + 1; i < n; i++)
                {
                    if (dense[i, j] != 0.0)
                    {
                        upperByCol.Add(dense[i, j]);
                        rowIndices.Add(i);
                    }
                }
            }

            _iptr = rowPtr.ToArray();
            _jptr = colIndices.ToArray();
            _altr = lowerVals.ToArray();
            _autr = upperByCol.ToArray();

            if (_altr.Length != _autr.Length || _altr.Length != _jptr.Length)
                throw new Exception("Несоответствие размеров при построении CSlR матрицы.");
        }

        public CsirMatrix(double[] di, double[] altr, double[] autr, int[] jptr, int[] iptr, int n)
        {
            if (di.Length != n)
                throw new ArgumentException("Длина di должна быть равна n");
            if (altr.Length != autr.Length || altr.Length != jptr.Length)
                throw new ArgumentException("altr, autr и jptr должны иметь одинаковую длину");
            if (iptr.Length != n + 1)
                throw new ArgumentException("iptr должен иметь длину n+1");
            Size = n;
            _di = (double[])di.Clone();
            _altr = (double[])altr.Clone();
            _autr = (double[])autr.Clone();
            _jptr = (int[])jptr.Clone();
            _iptr = (int[])iptr.Clone();
        }

        public double Get(int i, int j)
        {
            CheckIndices(i, j);
            if (i == j)
                return _di[i];
            else if (i > j)
            {
                int start = _iptr[i] - 1;
                int end = _iptr[i + 1] - 1;
                for (int k = start; k < end; k++)
                {
                    if (_jptr[k] == j)
                        return _altr[k];
                }
                return 0.0;
            }
            else
            {
                int start = _iptr[j] - 1;
                int end = _iptr[j + 1] - 1;
                for (int k = start; k < end; k++)
                {
                    if (_jptr[k] == i)
                        return _autr[k];
                }
                return 0.0;
            }
        }

        public void Set(int i, int j, double value)
        {
            CheckIndices(i, j);
            if (i == j)
            {
                _di[i] = value;
                return;
            }
            else if (i > j)
            {
                int start = _iptr[i] - 1;
                int end = _iptr[i + 1] - 1;
                int pos = -1;
                for (int k = start; k < end; k++)
                {
                    if (_jptr[k] == j)
                    {
                        pos = k;
                        break;
                    }
                }
                if (pos >= 0)
                {
                    if (value == 0.0)
                        RemoveAt(i, pos, true);
                    else
                        _altr[pos] = value;
                }
                else
                {
                    if (value != 0.0)
                        InsertAt(i, j, value, true);
                }
            }
            else
            {
                int start = _iptr[j] - 1;
                int end = _iptr[j + 1] - 1;
                int pos = -1;
                for (int k = start; k < end; k++)
                {
                    if (_jptr[k] == i)
                    {
                        pos = k;
                        break;
                    }
                }
                if (pos >= 0)
                {
                    if (value == 0.0)
                        RemoveAt(j, pos, false);
                    else
                        _autr[pos] = value;
                }
                else
                {
                    if (value != 0.0)
                        InsertAt(j, i, value, false);
                }
            }
        }

        public void AddToElement(int i, int j, double delta)
        {
            if (delta == 0.0) return;
            double current = Get(i, j);
            Set(i, j, current + delta);
        }

        public double this[int i, int j]
        {
            get => Get(i, j);
            set => Set(i, j, value);
        }

        private void InsertAt(int rowOrCol, int index, double value, bool isLower)
        {
            int insertPos = -1;
            int start = _iptr[rowOrCol] - 1;
            int end = _iptr[rowOrCol + 1] - 1;
            for (int k = start; k < end; k++)
            {
                int currentKey = _jptr[k];
                if (currentKey > index)
                {
                    insertPos = k;
                    break;
                }
            }
            if (insertPos == -1)
                insertPos = end;

            var valArray = isLower ? _altr : _autr;
            var newJptr = new List<int>(_jptr);
            var newVals = new List<double>(valArray);
            newJptr.Insert(insertPos, index);
            newVals.Insert(insertPos, value);
            _jptr = newJptr.ToArray();
            if (isLower)
                _altr = newVals.ToArray();
            else
                _autr = newVals.ToArray();

            for (int r = rowOrCol + 1; r <= Size; r++)
                _iptr[r]++;
        }

        private void RemoveAt(int rowOrCol, int pos, bool isLower)
        {
            var listJptr = new List<int>(_jptr);
            var listVals = new List<double>(isLower ? _altr : _autr);
            listJptr.RemoveAt(pos);
            listVals.RemoveAt(pos);
            _jptr = listJptr.ToArray();
            if (isLower)
                _altr = listVals.ToArray();
            else
                _autr = listVals.ToArray();

            for (int r = rowOrCol + 1; r <= Size; r++)
                _iptr[r]--;
        }

        private void CheckIndices(int i, int j)
        {
            if (i < 0 || i >= Size || j < 0 || j >= Size)
                throw new IndexOutOfRangeException($"Индексы ({i},{j}) вне диапазона [0..{Size - 1}]");
        }

        public double[] Multiply(double[] vector)
        {
            if (vector == null)
                throw new ArgumentNullException(nameof(vector));
            if (vector.Length != Size)
                throw new ArgumentException($"Длина вектора ({vector.Length}) не равна размеру матрицы ({Size})");

            double[] result = new double[Size];
            for (int i = 0; i < Size; i++)
                result[i] = _di[i] * vector[i];

            for (int i = 0; i < Size; i++)
            {
                int start = _iptr[i] - 1;
                int end = _iptr[i + 1] - 1;
                for (int k = start; k < end; k++)
                {
                    int j = _jptr[k];
                    double aij = _altr[k];
                    result[i] += aij * vector[j];
                }
            }

            for (int j = 0; j < Size; j++)
            {
                int start = _iptr[j] - 1;
                int end = _iptr[j + 1] - 1;
                for (int k = start; k < end; k++)
                {
                    int i = _jptr[k];
                    double aij = _autr[k];
                    result[i] += aij * vector[j];
                }
            }

            return result;
        }

        public double[,] ToDense()
        {
            var dense = new double[Size, Size];
            for (int i = 0; i < Size; i++)
                dense[i, i] = _di[i];

            for (int i = 0; i < Size; i++)
            {
                int start = _iptr[i] - 1;
                int end = _iptr[i + 1] - 1;
                for (int k = start; k < end; k++)
                {
                    int j = _jptr[k];
                    dense[i, j] = _altr[k];
                }
            }

            for (int j = 0; j < Size; j++)
            {
                int start = _iptr[j] - 1;
                int end = _iptr[j + 1] - 1;
                for (int k = start; k < end; k++)
                {
                    int i = _jptr[k];
                    dense[i, j] = _autr[k];
                }
            }

            return dense;
        }

        public CsirMatrix Add(CsirMatrix other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));
            if (other.Size != Size)
                throw new ArgumentException("Размеры матриц должны совпадать");

            var dense1 = this.ToDense();
            var dense2 = other.ToDense();
            var resultDense = new double[Size, Size];
            for (int i = 0; i < Size; i++)
                for (int j = 0; j < Size; j++)
                    resultDense[i, j] = dense1[i, j] + dense2[i, j];
            return new CsirMatrix(resultDense);
        }
    }
}