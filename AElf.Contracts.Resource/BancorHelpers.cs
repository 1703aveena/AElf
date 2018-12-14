using System;
using System.Collections;
using Google.Protobuf.WellKnownTypes;
using static System.Decimal;

namespace AElf.Contracts.Resource
{
    public static class BancorHelpers
    {
        public static ulong BuyResourceFromExchange(this ResourceContract c, string resourceType, ulong paidElf)
        {
            checked
            {
                ResourceContract.AssertCorrectResourceType(resourceType);
                var rt = new StringValue() {Value = resourceType};
                var connector = c.ConnectorPairs[rt];
                var tokensIssued = ToSmartToken(paidElf, connector.ElfBalance);
                var resourcePayout = (ulong) FromSmartToken(tokensIssued, connector.ResBalance);
                connector.ElfBalance += paidElf;
                connector.ResBalance -= resourcePayout;
                c.ConnectorPairs[rt] = connector;
                return resourcePayout;
            }
        }

        public static ulong SellResourceToExchange(this ResourceContract c, string resourceType, ulong paidRes)
        {
            checked
            {
                ResourceContract.AssertCorrectResourceType(resourceType);
                var rt = new StringValue() {Value = resourceType};
                var connector = c.ConnectorPairs[rt];
                var tokensIssued = ToSmartToken(paidRes, connector.ResBalance);
                var elfPayout = (ulong) FromSmartToken(tokensIssued, connector.ElfBalance);
                connector.ElfBalance -= elfPayout;
                connector.ResBalance += paidRes;
                c.ConnectorPairs[rt] = connector;
                return elfPayout;
            }
        }

        private static decimal ToSmartToken(decimal connected, decimal balance)
        {
            decimal s = 10000000000;
            decimal c = connected;
            decimal b = balance;
            decimal w = new decimal(5, 0, 0, false, 1);
            decimal x = One + c / b;
            var tokensIssued = s * (Exp(w * Ln(x)) - One);
            return tokensIssued;
        }

        private static decimal FromSmartToken(decimal destroyed, decimal balance)
        {
            decimal s = 10000000000;
            decimal d = destroyed;
            decimal b = balance;
            decimal w = new decimal(5, 0, 0, false, 1);
            // x^y = exp(ln(x^y)) = exp(y*ln(x))
            decimal x = One + d / s;
            decimal y = One / w;
            var payout = b * (Exp(y * Ln(x)) - One);
            return payout;
        }

        #region Exponential Helpers

        static readonly uint _LOOPS = 22; // Max = 22

        private static ulong[] Fact = new ulong[]
        {
            1L,
            1L * 2,
            1L * 2 * 3,
            1L * 2 * 3 * 4,
            1L * 2 * 3 * 4 * 5,
            1L * 2 * 3 * 4 * 5 * 6,
            1L * 2 * 3 * 4 * 5 * 6 * 7,
            1L * 2 * 3 * 4 * 5 * 6 * 7 * 8,
            1L * 2 * 3 * 4 * 5 * 6 * 7 * 8 * 9,
            1L * 2 * 3 * 4 * 5 * 6 * 7 * 8 * 9 * 10,
            1L * 2 * 3 * 4 * 5 * 6 * 7 * 8 * 9 * 10 * 11,
            1L * 2 * 3 * 4 * 5 * 6 * 7 * 8 * 9 * 10 * 11 * 12,
            1L * 2 * 3 * 4 * 5 * 6 * 7 * 8 * 9 * 10 * 11 * 12 * 13,
            1L * 2 * 3 * 4 * 5 * 6 * 7 * 8 * 9 * 10 * 11 * 12 * 13 * 14,
            1L * 2 * 3 * 4 * 5 * 6 * 7 * 8 * 9 * 10 * 11 * 12 * 13 * 14 * 15,
            1L * 2 * 3 * 4 * 5 * 6 * 7 * 8 * 9 * 10 * 11 * 12 * 13 * 14 * 15 * 16,
            1L * 2 * 3 * 4 * 5 * 6 * 7 * 8 * 9 * 10 * 11 * 12 * 13 * 14 * 15 * 16 * 17,
            1L * 2 * 3 * 4 * 5 * 6 * 7 * 8 * 9 * 10 * 11 * 12 * 13 * 14 * 15 * 16 * 17 * 18,
            1L * 2 * 3 * 4 * 5 * 6 * 7 * 8 * 9 * 10 * 11 * 12 * 13 * 14 * 15 * 16 * 17 * 18 * 19,
            1L * 2 * 3 * 4 * 5 * 6 * 7 * 8 * 9 * 10 * 11 * 12 * 13 * 14 * 15 * 16 * 17 * 18 * 19 * 20,
            14197454024290336768L, //1L * 2 * 3 * 4 * 5 * 6 * 7 * 8 * 9 * 10 * 11 * 12 * 13 * 14 * 15 * 16 * 17 * 18 * 19 * 20 * 21,        // NOTE: Overflow during compilation
            17196083355034583040L, //1L * 2 * 3 * 4 * 5 * 6 * 7 * 8 * 9 * 10 * 11 * 12 * 13 * 14 * 15 * 16 * 17 * 18 * 19 * 20 * 21 * 22    // NOTE: Overflow during compilation
        };

        // http://www.daimi.au.dk/~ivan/FastExpproject.pdf
        // Left to Right Binary Exponentiation
        public static decimal Pow(decimal x, uint y)
        {
            if (y == 1)
                return x;

            decimal A = 1m;
            BitArray e = new BitArray(BitConverter.GetBytes(y));
            int t = e.Count;

            for (int i = t - 1; i >= 0; --i)
            {
                A *= A;
                if (e[i] == true)
                {
                    A *= x;
                }
            }

            return A;
        }

        // http://stackoverflow.com/questions/429165/raising-a-decimal-to-a-power-of-decimal
        // natural logarithm series
        private static decimal Ln(decimal a)
        {
            /*
            ln(a) = log(1-x) = - x - x^2/2 - x^3/3 - ...   (where |x| < 1)
                x: a = 1-x    =>   x = 1-a = 1 - 1.004 = -.004
            */
            decimal x = 1 - a;
            if (Math.Abs(x) >= 1)
                throw new Exception("must be 0 < a < 2");

            decimal result = 0;
            uint iteration = _LOOPS;
            while (iteration > 0)
            {
                result -= Pow(x, iteration) / iteration;
                iteration--;
            }

            return result;
        }


        // http://stackoverflow.com/questions/429165/raising-a-decimal-to-a-power-of-decimal
        // power series
        private static decimal Exp(decimal y)
        {
            /*
            exp(y) = 1 + y + y^2/2 + x^3/3! + y^4/4! + y^5/5! + ...
            */

            uint iteration = _LOOPS;
            decimal result = 1;
            while (iteration > 0)
            {
                //uint fatorial = Factorial(iteration);
                ulong fatorial = Fact[iteration - 1];
                result += (Pow(y, iteration) / fatorial);
                iteration--;
            }

            return result;
        }

        #endregion Exponential Helpers
    }
}