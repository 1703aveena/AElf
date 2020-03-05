using System;
using System.Collections.Generic;

namespace AElf.Kernel.FeeCalculation
{
    public class PieceCalculateFunction
    {
        private PieceCalculateFunction _next;
        private Func<int[], int, long> _currentCalculateFunction;

        public PieceCalculateFunction(Func<int[], int, long> function)
        {
            _currentCalculateFunction = function;
        }

        public PieceCalculateFunction()
        {
        }

        public PieceCalculateFunction AddFunction(Func<int[], int, long> function)
        {
            if (_currentCalculateFunction == null)
            {
                _currentCalculateFunction = function;
                return this;
            }

            _next = new PieceCalculateFunction(function);
            return _next;
        }

        public long CalculateFee(IList<int[]> coefficient, int totalCount, int currentCoefficientIndex = 0)
        {
            if (coefficient.Count == 0) return 0;
            var currentCoefficient = coefficient[currentCoefficientIndex];
            var piece = currentCoefficient[0];
            if (piece >= totalCount || _next == null || coefficient.Count == 1 ||
                currentCoefficientIndex >= coefficient.Count)
            {
                // totalCount will be decreased during calling this method recursively,
                // finally piece will greater than or equal to totalCount, thus terminate the recursion.
                // And this is the way to implement piece-wise function.
                return _currentCalculateFunction(currentCoefficient, totalCount);
            }

            var nextCoefficientIndex = currentCoefficientIndex + 1;
            var nextCount = totalCount - piece;
            nextCount = nextCount > 0 ? nextCount : 0;
            return _currentCalculateFunction(currentCoefficient, piece) +
                   _next.CalculateFee(coefficient, nextCount, nextCoefficientIndex);
        }
    }
}