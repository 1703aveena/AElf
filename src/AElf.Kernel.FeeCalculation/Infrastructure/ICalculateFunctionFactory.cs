﻿using System;
using Volo.Abp.DependencyInjection;

namespace AElf.Kernel.FeeCalculation.Infrastructure
{
    /// <summary>
    /// To provide basic function for piece-wise function.
    /// </summary>
    public interface ICalculateFunctionFactory
    {
        Func<int, long> GetFunction(params int[] parameters);
    }

    public class CalculateFunctionFactory : ICalculateFunctionFactory, ISingletonDependency
    {
        private const decimal Precision = 100000000;
        private const int Liner = 0;
        private const int Power = 1;

        public Func<int, long> GetFunction(params int[] parameters)
        {
            if (parameters[0] == Liner)
            {
                return GetLinerFunction(parameters);
            }

            return GetPowerFunction(parameters);
        }

        private Func<int, long> GetLinerFunction(int[] coefficient)
        {
            return count => LinerFunction(coefficient, count);
        }

        private Func<int, long> GetPowerFunction(int[] coefficient)
        {
            return count => PowerFunction(coefficient, count);
        }

        private long LinerFunction(int[] coefficient, int count)
        {
            if (coefficient.Length != 5)
                throw new ArgumentException($"Invalid coefficient count, should be 5, but is {coefficient.Length}");
            var outcome = Precision * count * coefficient[2] / coefficient[3] + coefficient[4];
            return (long) outcome;
        }

        private long PowerFunction(int[] coefficient, int count)
        {
            if (coefficient.Length != 8)
                throw new ArgumentException($"Invalid coefficient count, should be 8, but is {coefficient.Length}");
            var outcome = Precision * (decimal) Math.Pow((double) count / coefficient[5], coefficient[4]) *
                          coefficient[6] / coefficient[7] +
                          Precision * coefficient[2] * count / coefficient[3];
            return (long) outcome;
        }
    }
}