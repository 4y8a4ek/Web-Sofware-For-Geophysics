using System;
using System.Collections.Generic;
using System.Linq;

namespace MeshDataStructures.Models
{
    /// <summary>
    /// Разреженная матрица в формате CSR (Compressed Sparse Row).
    /// </summary>
    /// <remarks>
    /// Представляет матрицу с эффективным хранением ненулевых элементов.
    /// Используется для работы с большими разреженными матрицами в геофизических задачах.
    /// </remarks>
    public class CsrMatrix
    {
        // Внутренние хранилища
        private List<double> _values;
        private List<int> _colIndices;
        private List<int> _rowPtr;

        /// <summary>Число строк матрицы.</summary>
        public int Rows { get; private set; }

        /// <summary>Число столбцов матрицы.</summary>
        public int Cols { get; private set; }

        /// <summary>Количество ненулевых элементов в матрице.</summary>
        public int NonZeros => _values.Count;

        #region Конструкторы

        /// <summary>
        /// Создание пустой матрицы заданного размера (все элементы равны нулю).
        /// </summary>
        /// <param name="rows">Число строк (должно быть положительным).</param>
        /// <param name="cols">Число столбцов (должно быть положительным).</param>
        /// <exception cref="ArgumentException">Выбрасывается, если rows или cols ≤ 0.</exception>
        public CsrMatrix(int rows, int cols)
        {
            if (rows <= 0 || cols <= 0)
                throw new ArgumentException("Размеры матрицы должны быть положительными");
            Rows = rows;
            Cols = cols;
            _values = new List<double>();
            _colIndices = new List<int>();
            _rowPtr = new List<int>(new int[rows + 1]); // все нули
        }

        /// <summary>
        /// Создание матрицы из плотного двумерного массива.
        /// </summary>
        /// <param name="dense">Двумерный массив double.</param>
        /// <exception cref="ArgumentNullException">Выбрасывается, если dense == null.</exception>
        public CsrMatrix(double[,] dense)
        {
            if (dense == null)
                throw new ArgumentNullException(nameof(dense));
            Rows = dense.GetLength(0);
            Cols = dense.GetLength(1);
            _values = new List<double>();
            _colIndices = new List<int>();
            _rowPtr = new List<int> { 0 };

            for (int i = 0; i < Rows; i++)
            {
                for (int j = 0; j < Cols; j++)
                {
                    double val = dense[i, j];
                    if (val != 0.0)
                    {
                        _values.Add(val);
                        _colIndices.Add(j);
                    }
                }
                _rowPtr.Add(_values.Count);
            }
        }

        /// <summary>
        /// Создание матрицы из готовых CSR-компонент.
        /// </summary>
        /// <param name="values">Массив ненулевых значений.</param>
        /// <param name="colIndices">Массив индексов столбцов для каждого значения.</param>
        /// <param name="rowPtr">Массив указателей начала строк (длина rows+1).</param>
        /// <param name="rows">Число строк.</param>
        /// <param name="cols">Число столбцов.</param>
        /// <exception cref="ArgumentException">
        /// Выбрасывается, если rowPtr.Length != rows+1 или values.Length != colIndices.Length.
        /// </exception>
        public CsrMatrix(double[] values, int[] colIndices, int[] rowPtr, int rows, int cols)
        {
            if (rowPtr.Length != rows + 1)
                throw new ArgumentException("rowPtr должен иметь длину rows+1");
            if (values.Length != colIndices.Length)
                throw new ArgumentException("values и colIndices должны быть одной длины");

            Rows = rows;
            Cols = cols;
            _values = new List<double>(values);
            _colIndices = new List<int>(colIndices);
            _rowPtr = new List<int>(rowPtr);
        }

        #endregion

        #region Доступ к элементам

        /// <summary>
        /// Получить значение элемента (i, j).
        /// </summary>
        /// <param name="i">Индекс строки (0-based).</param>
        /// <param name="j">Индекс столбца (0-based).</param>
        /// <returns>Значение элемента, или 0.0, если элемент отсутствует.</returns>
        /// <exception cref="IndexOutOfRangeException">Выбрасывается при недопустимых индексах.</exception>
        public double Get(int i, int j)
        {
            CheckIndices(i, j);
            int start = _rowPtr[i];
            int end = _rowPtr[i + 1];
            int pos = _colIndices.BinarySearch(start, end - start, j, Comparer<int>.Default);
            if (pos >= 0)
                return _values[pos];
            return 0.0;
        }

        /// <summary>
        /// Установить значение элемента (i, j).
        /// </summary>
        /// <param name="i">Индекс строки (0-based).</param>
        /// <param name="j">Индекс столбца (0-based).</param>
        /// <param name="value">Новое значение. Если равно 0, элемент удаляется.</param>
        /// <exception cref="IndexOutOfRangeException">Выбрасывается при недопустимых индексах.</exception>
        public void Set(int i, int j, double value)
        {
            CheckIndices(i, j);
            int start = _rowPtr[i];
            int end = _rowPtr[i + 1];
            int pos = _colIndices.BinarySearch(start, end - start, j, Comparer<int>.Default);

            if (pos >= 0)
            {
                if (value == 0.0)
                    RemoveAt(i, pos);
                else
                    _values[pos] = value;
            }
            else
            {
                if (value != 0.0)
                    InsertAt(i, ~pos, j, value);
            }
        }

        /// <summary>
        /// Добавить значение к существующему элементу или создать новый, если его не было.
        /// </summary>
        /// <param name="i">Индекс строки (0-based).</param>
        /// <param name="j">Индекс столбца (0-based).</param>
        /// <param name="delta">Приращение. Если результат становится 0, элемент удаляется.</param>
        /// <exception cref="IndexOutOfRangeException">Выбрасывается при недопустимых индексах.</exception>
        public void AddToElement(int i, int j, double delta)
        {
            if (delta == 0.0) return;
            CheckIndices(i, j);
            int start = _rowPtr[i];
            int end = _rowPtr[i + 1];
            int pos = _colIndices.BinarySearch(start, end - start, j, Comparer<int>.Default);

            if (pos >= 0)
            {
                double newVal = _values[pos] + delta;
                if (newVal == 0.0)
                    RemoveAt(i, pos);
                else
                    _values[pos] = newVal;
            }
            else
            {
                InsertAt(i, ~pos, j, delta);
            }
        }

        /// <summary>
        /// Индексатор для удобного доступа (чтение и запись).
        /// </summary>
        /// <param name="i">Индекс строки (0-based).</param>
        /// <param name="j">Индекс столбца (0-based).</param>
        /// <returns>Значение элемента.</returns>
        public double this[int i, int j]
        {
            get => Get(i, j);
            set => Set(i, j, value);
        }

        #endregion

        #region Вспомогательные методы для изменения структуры CSR

        private void InsertAt(int row, int index, int col, double value)
        {
            _values.Insert(index, value);
            _colIndices.Insert(index, col);
            for (int r = row + 1; r <= Rows; r++)
                _rowPtr[r]++;
        }

        private void RemoveAt(int row, int index)
        {
            _values.RemoveAt(index);
            _colIndices.RemoveAt(index);
            for (int r = row + 1; r <= Rows; r++)
                _rowPtr[r]--;
        }

        private void CheckIndices(int i, int j)
        {
            if (i < 0 || i >= Rows || j < 0 || j >= Cols)
                throw new IndexOutOfRangeException($"Индекс ({i},{j}) вне диапазона [0..{Rows - 1}]x[0..{Cols - 1}]");
        }

        #endregion

        #region Матрично-векторное умножение

        /// <summary>
        /// Умножение матрицы на вектор-столбец.
        /// </summary>
        /// <param name="vector">Вектор длины Cols.</param>
        /// <returns>Вектор-результат длины Rows.</returns>
        /// <exception cref="ArgumentNullException">Если vector == null.</exception>
        /// <exception cref="ArgumentException">Если длина вектора не равна Cols.</exception>
        public double[] Multiply(double[] vector)
        {
            if (vector == null)
                throw new ArgumentNullException(nameof(vector));
            if (vector.Length != Cols)
                throw new ArgumentException($"Длина вектора ({vector.Length}) не равна числу столбцов ({Cols})");

            double[] result = new double[Rows];
            for (int i = 0; i < Rows; i++)
            {
                double sum = 0.0;
                int start = _rowPtr[i];
                int end = _rowPtr[i + 1];
                for (int idx = start; idx < end; idx++)
                {
                    sum += _values[idx] * vector[_colIndices[idx]];
                }
                result[i] = sum;
            }
            return result;
        }

        #endregion

        #region Дополнительные операции

        /// <summary>
        /// Преобразование в плотную матрицу (для отладки или визуализации).
        /// </summary>
        /// <returns>Двумерный массив double размера Rows x Cols.</returns>
        public double[,] ToDense()
        {
            var dense = new double[Rows, Cols];
            for (int i = 0; i < Rows; i++)
            {
                int start = _rowPtr[i];
                int end = _rowPtr[i + 1];
                for (int idx = start; idx < end; idx++)
                {
                    dense[i, _colIndices[idx]] = _values[idx];
                }
            }
            return dense;
        }

        /// <summary>
        /// Сложение двух матриц одинакового размера.
        /// </summary>
        /// <param name="other">Вторая матрица.</param>
        /// <returns>Новая матрица, являющаяся поэлементной суммой.</returns>
        /// <exception cref="ArgumentNullException">Если other == null.</exception>
        /// <exception cref="ArgumentException">Если размеры матриц не совпадают.</exception>
        public CsrMatrix Add(CsrMatrix other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));
            if (other.Rows != Rows || other.Cols != Cols)
                throw new ArgumentException("Размеры матриц должны совпадать");

            var result = new CsrMatrix(Rows, Cols);
            for (int i = 0; i < Rows; i++)
            {
                var dict = new Dictionary<int, double>();
                int start1 = _rowPtr[i], end1 = _rowPtr[i + 1];
                int start2 = other._rowPtr[i], end2 = other._rowPtr[i + 1];

                int p1 = start1, p2 = start2;
                while (p1 < end1 && p2 < end2)
                {
                    int c1 = _colIndices[p1];
                    int c2 = other._colIndices[p2];
                    if (c1 == c2)
                    {
                        double sum = _values[p1] + other._values[p2];
                        if (sum != 0.0)
                            dict[c1] = sum;
                        p1++; p2++;
                    }
                    else if (c1 < c2)
                    {
                        dict[c1] = _values[p1];
                        p1++;
                    }
                    else
                    {
                        dict[c2] = other._values[p2];
                        p2++;
                    }
                }
                while (p1 < end1)
                {
                    dict[_colIndices[p1]] = _values[p1];
                    p1++;
                }
                while (p2 < end2)
                {
                    dict[other._colIndices[p2]] = other._values[p2];
                    p2++;
                }

                foreach (var kv in dict.OrderBy(k => k.Key))
                {
                    result._values.Add(kv.Value);
                    result._colIndices.Add(kv.Key);
                }
                result._rowPtr.Add(result._values.Count);
            }
            return result;
        }

        #endregion
    }
}