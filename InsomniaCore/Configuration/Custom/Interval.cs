using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Configuration
{
    [TypeConverter(typeof(IntervalConverter))]
    public readonly struct Interval(TimeSpan duration)
    {
        public readonly TimeSpan Duration { get; init; } = duration;

        public override string ToString() => Duration.ToString();
    }

    public class IntervalConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type type)
        {
            return type == typeof(string);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string str)
            {
                return new Interval { Duration = ParseDuration(str) };
            }

            return null;
        }

        internal static TimeSpan ParseDuration(string str)
        {
            if (str.Contains('h'))
            {
                var split = str.Split("h");

                return TimeSpan.FromHours(int.Parse(split[0]));
            }

            if (str.Contains("min"))
            {
                var split = str.Split("min");

                return TimeSpan.FromMinutes(int.Parse(split[0]));
            }

            if (str.Contains('s'))
            {
                var split = str.Split("s");

                return TimeSpan.FromSeconds(int.Parse(split[0]));
            }

            return TimeSpan.Parse(str); // parse 01:02:03 -> 1h 2m 3s
        }
    }
}
