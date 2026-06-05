using System;

namespace HandOfGod.Gestures
{
    public sealed class OneEuroFilter
    {
        private readonly double minCutoff;
        private readonly double beta;
        private readonly double dCutoff;
        private readonly LowPassFilter valueFilter = new LowPassFilter();
        private readonly LowPassFilter derivativeFilter = new LowPassFilter();
        private bool hasLastRawValue;
        private double lastRawValue;

        public OneEuroFilter(double minCutoff = 1.0, double beta = 0.04, double dCutoff = 1.0)
        {
            this.minCutoff = minCutoff;
            this.beta = beta;
            this.dCutoff = dCutoff;
        }

        public double Filter(double value, double rate)
        {
            if (rate <= 0.0)
            {
                return value;
            }

            var derivative = 0.0;
            if (hasLastRawValue)
            {
                derivative = (value - lastRawValue) * rate;
            }
            else
            {
                hasLastRawValue = true;
            }

            lastRawValue = value;
            var filteredDerivative = derivativeFilter.Filter(derivative, Alpha(rate, dCutoff));
            var cutoff = minCutoff + beta * Math.Abs(filteredDerivative);
            return valueFilter.Filter(value, Alpha(rate, cutoff));
        }

        private static double Alpha(double rate, double cutoff)
        {
            var tau = 1.0 / (2.0 * Math.PI * cutoff);
            var te = 1.0 / rate;
            return 1.0 / (1.0 + tau / te);
        }
    }

    internal sealed class LowPassFilter
    {
        private bool initialized;
        private double state;

        public double Filter(double value, double alpha)
        {
            if (!initialized)
            {
                initialized = true;
                state = value;
                return value;
            }

            state = alpha * value + (1.0 - alpha) * state;
            return state;
        }
    }
}
