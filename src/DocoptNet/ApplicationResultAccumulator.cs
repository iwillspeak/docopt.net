#nullable enable

namespace DocoptNet
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    interface IApplicationResultAccumulator<T>
    {
        T New();
        T Command(T state, string name, bool value);
        T Command(T state, string name, int value);
        T Argument(T state, string name);
        T Argument(T state, string name, string value);
        T Argument(T state, string name, StringList value);
        T Option(T state, string name);
        T Option(T state, string name, bool value);
        T Option(T state, string name, string value);
        T Option(T state, string name, int value);
        T Option(T state, string name, StringList value);
        T Error(DocoptBaseException exception);
    }

    static class ApplicationResultAccumulators
    {
        public static readonly IApplicationResultAccumulator<IDictionary<string, Value>> ValueDictionary = new ValueDictionaryAccumulator();
        public static readonly IApplicationResultAccumulator<IDictionary<string, ValueObject>> ValueObjectDictionary = new ValueObjectDictionaryAccumulator();

        public static IApplicationResultAccumulator<T> ForType<T>()
            where T: new()
        {
            return new TypedArgumentsAccumulator<T>();
        }

        sealed class ValueDictionaryAccumulator : IApplicationResultAccumulator<IDictionary<string, Value>>
        {
            public IDictionary<string, Value> New() => new Dictionary<string, Value>();
            public IDictionary<string, Value> Command(IDictionary<string, Value> state, string name, bool value) => Adding(state, name, value);
            public IDictionary<string, Value> Command(IDictionary<string, Value> state, string name, int value) => Adding(state, name, value);
            public IDictionary<string, Value> Argument(IDictionary<string, Value> state, string name) => Adding(state, name, Value.None);
            public IDictionary<string, Value> Argument(IDictionary<string, Value> state, string name, string value) => Adding(state, name, value);
            public IDictionary<string, Value> Argument(IDictionary<string, Value> state, string name, StringList value) => Adding(state, name, value);
            public IDictionary<string, Value> Option(IDictionary<string, Value> state, string name) => Adding(state, name, Value.None);
            public IDictionary<string, Value> Option(IDictionary<string, Value> state, string name, bool value) => Adding(state, name, value);
            public IDictionary<string, Value> Option(IDictionary<string, Value> state, string name, string value) => Adding(state, name, value);
            public IDictionary<string, Value> Option(IDictionary<string, Value> state, string name, int value) => Adding(state, name, value);
            public IDictionary<string, Value> Option(IDictionary<string, Value> state, string name, StringList value) => Adding(state, name, value);
            public IDictionary<string, Value> Error(DocoptBaseException exception) => null!;

            static IDictionary<string, Value> Adding(IDictionary<string, Value> dict, string name, Value value)
            {
                dict[name] = value;
                return dict;
            }
        }

        sealed class ValueObjectDictionaryAccumulator : IApplicationResultAccumulator<IDictionary<string, ValueObject>>
        {
            public IDictionary<string, ValueObject> New() => new Dictionary<string, ValueObject>();
            public IDictionary<string, ValueObject> Command(IDictionary<string, ValueObject> state, string name, bool value) => Adding(state, name, value);
            public IDictionary<string, ValueObject> Command(IDictionary<string, ValueObject> state, string name, int value) => Adding(state, name, value);
            public IDictionary<string, ValueObject> Argument(IDictionary<string, ValueObject> state, string name) => Adding(state, name, null);
            public IDictionary<string, ValueObject> Argument(IDictionary<string, ValueObject> state, string name, string value) => Adding(state, name, value);
            public IDictionary<string, ValueObject> Argument(IDictionary<string, ValueObject> state, string name, StringList value) => Adding(state, name, value);
            public IDictionary<string, ValueObject> Option(IDictionary<string, ValueObject> state, string name) => Adding(state, name, null);
            public IDictionary<string, ValueObject> Option(IDictionary<string, ValueObject> state, string name, bool value) => Adding(state, name, value);
            public IDictionary<string, ValueObject> Option(IDictionary<string, ValueObject> state, string name, string value) => Adding(state, name, value);
            public IDictionary<string, ValueObject> Option(IDictionary<string, ValueObject> state, string name, int value) => Adding(state, name, value);
            public IDictionary<string, ValueObject> Option(IDictionary<string, ValueObject> state, string name, StringList value) => Adding(state, name, value);
            public IDictionary<string, ValueObject> Error(DocoptBaseException exception) => null!;

            static IDictionary<string, ValueObject> Adding(IDictionary<string, ValueObject> dict, string name, object? value)
            {
                dict[name] = new ValueObject(value);
                return dict;
            }
        }

        sealed class TypedArgumentsAccumulator<T> : IApplicationResultAccumulator<T>
            where T: new()
        {
            public T New() => new T();
            public T Command(T state, string name, bool value) => Adding(state, name, value);
            public T Command(T state, string name, int value) => Adding(state, name, value);
            public T Argument(T state, string name) => Adding(state, name, null);
            public T Argument(T state, string name, string value) => Adding(state, name, value);
            public T Argument(T state, string name, StringList value) => Adding(state, name, value);
            public T Option(T state, string name) => Adding(state, name, null);
            public T Option(T state, string name, bool value) => Adding(state, name, value);
            public T Option(T state, string name, string value) => Adding(state, name, value);
            public T Option(T state, string name, int value) => Adding(state, name, value);
            public T Option(T state, string name, StringList value) => Adding(state, name, value);
            public T Error(DocoptBaseException exception) => default!;

            static T Adding(T args, string name, object? value)
            {
                var propertyName = GetPropertyName(name);
                if (!s_Properties.TryGetValue(propertyName, out var prop))
                {
                    throw new ArgumentException($"Can't find property ${propertyName} for argument {name}", nameof(name));
                }
                prop.SetValue(args, value);
                return args;
            }

            private static string GetPropertyName(string name)
            {
                if (name.StartsWith("--"))
                {
                    return $"Flag{Pascalise(name.Substring(2))}";
                }
                if (name.StartsWith("<") && name.EndsWith(">"))
                {
                    return $"Arg{Pascalise(name.Substring(1, name.Length - 2))}";
                }
                return Pascalise(name);
            }

            private static string Pascalise(string kebabString)
            {
                var pascalBuilder = new StringBuilder();
                var boundary = true;
                foreach (var c in kebabString)
                {
                    if (c == '-')
                    {
                        boundary = true;
                    }
                    else
                    {
                        pascalBuilder.Append(boundary ? char.ToUpper(c) : c);
                        boundary = false;
                    }
                }
                return pascalBuilder.ToString();
            }

            private static IDictionary<string, PropertyInfo> s_Properties =
                typeof(T).GetTypeInfo().GetProperties().ToDictionary(p => p.Name);
        }
    }
}
