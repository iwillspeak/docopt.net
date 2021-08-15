#nullable enable

namespace DocoptNet.Tests
{
    using System.Collections.Generic;
    using NUnit.Framework;

    [TestFixture]
    public class ApplicationResultAccumulator
    {
        public class ValueObjectDictionary
        {
            static readonly IApplicationResultAccumulator<IDictionary<string, ValueObject>> Accumulator = ApplicationResultAccumulators.ValueObjectDictionary;

            [Test]
            public void New_returns_empty_dictionary()
            {
                var dict = Accumulator.New();
                Assert.That(dict, Is.Empty);
            }

            [Test]
            public void Command_adds_entry_with_value_converted_to_object()
            {
                var dict = Accumulator.New();
                dict = Accumulator.Command(dict, "command", true);
                var value = dict["command"];
                Assert.That(value, Is.InstanceOf<ValueObject>());
                Assert.That(value.Value, Is.EqualTo(true));
            }

            [Test]
            public void Argument_adds_entry_with_value_converted_to_object()
            {
                var dict = Accumulator.New();
                dict = Accumulator.Argument(dict, "<argument>", "value");
                var value = dict["<argument>"];
                Assert.That(value, Is.InstanceOf<ValueObject>());
                Assert.That(value.Value, Is.EqualTo("value"));
            }

            [Test]
            public void Option_adds_entry_with_value_converted_to_object()
            {
                var dict = Accumulator.New();
                dict = Accumulator.Argument(dict, "--option", "value");
                var value = dict["--option"];
                Assert.That(value, Is.InstanceOf<ValueObject>());
                Assert.That(value.Value, Is.EqualTo("value"));
            }

            [Test]
            public void Error_returns_null()
            {
                Assert.That(Accumulator.Error(new DocoptInputErrorException()), Is.Null);
            }
        }

        [TestFixture]
        public class ValueDictionary
        {
            static readonly IApplicationResultAccumulator<IDictionary<string, Value>> Accumulator = ApplicationResultAccumulators.ValueDictionary;

            [Test]
            public void New_returns_empty_dictionary()
            {
                var dict = Accumulator.New();
                Assert.That(dict, Is.Empty);
            }

            [Test]
            public void Command_adds_entry_with_value()
            {
                var dict = Accumulator.New();
                dict = Accumulator.Command(dict, "command", true);
                var value = dict["command"];
                Assert.That(value, Is.InstanceOf<Value>());
                Assert.That((bool)value, Is.EqualTo(true));
            }

            [Test]
            public void Argument_adds_entry_with_value()
            {
                var dict = Accumulator.New();
                dict = Accumulator.Argument(dict, "<argument>", "value");
                var value = dict["<argument>"];
                Assert.That(value, Is.InstanceOf<Value>());
                Assert.That((string)value, Is.EqualTo("value"));
            }

            [Test]
            public void Option_adds_entry_with_value()
            {
                var dict = Accumulator.New();
                dict = Accumulator.Argument(dict, "--option", "value");
                var value = dict["--option"];
                Assert.That(value, Is.InstanceOf<Value>());
                Assert.That((string)value, Is.EqualTo("value"));
            }

            [Test]
            public void Error_returns_null()
            {
                Assert.That(Accumulator.Error(new DocoptInputErrorException()), Is.Null);
            }
        }



        [TestFixture]
        public class TypedArguments
        {
            static readonly IApplicationResultAccumulator<TestArguments> Accumulator = ApplicationResultAccumulators.ForType<TestArguments>();

            private class TestArguments
            {
                public bool Command { get; set; }
                public string? ArgArgument { get; set; }
                public string? FlagOption { get; set; }
                public int FlagMaxDegreeOfParallelism { get; set; }
            }

            [Test]
            public void Command_adds_entry_with_value()
            {
                var args = Accumulator.New();
                args = Accumulator.Command(args, "command", true);
                var value = args.Command;
                Assert.That(value, Is.EqualTo(true));
            }

            [Test]
            public void Argument_adds_entry_with_value()
            {
                var args = Accumulator.New();
                args = Accumulator.Argument(args, "<argument>", "value");
                var value = args.ArgArgument;
                Assert.That(value, Is.EqualTo("value"));
            }

            [Test]
            public void Option_adds_entry_with_value()
            {
                var args = Accumulator.New();
                args = Accumulator.Option(args, "--option", "value");
                var value = args.FlagOption;
                Assert.That(value, Is.EqualTo("value"));
            }

            [Test]
            public void Option_with_multiple_words_is_pascalised()
            {
                var args = Accumulator.New();
                args = Accumulator.Option(args, "--max-degree-of-parallelism", 101);
                var value = args.FlagMaxDegreeOfParallelism;
                Assert.That(value, Is.EqualTo(101));
            }

            [Test]
            public void Error_returns_null()
            {
                Assert.That(Accumulator.Error(new DocoptInputErrorException()), Is.Null);
            }
        }
    }
}
