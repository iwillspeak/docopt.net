``docopt.net`` is a .NET port of docopt
======================================================================

Isn't it awesome how `CommandLineParser <https://nuget.org/packages/CommandLineParser/>`_ and `PowerArgs <https://nuget.org/packages/PowerArgs/>`_ generate help
messages based on your code?!

*Hell no!*  You know what's awesome?  It's when the option parser *is*
generated based on the beautiful help message that you write yourself!
This way you don't need to write this stupid repeatable parser-code,
and instead can write only the help message--*the way you want it*.

**docopt.net** helps you create most beautiful command-line interfaces
*easily*:

.. code:: c#

  using System;
  using DocoptNet;

  namespace NavalFate
  {
      internal class Program
      {
          private const string usage = @"Naval Fate.

      Usage:
        naval_fate.exe ship new <name>...
        naval_fate.exe ship <name> move <x> <y> [--speed=<kn>]
        naval_fate.exe ship shoot <x> <y>
        naval_fate.exe mine (set|remove) <x> <y> [--moored | --drifting]
        naval_fate.exe (-h | --help)
        naval_fate.exe --version

      Options:
        -h --help     Show this screen.
        --version     Show version.
        --speed=<kn>  Speed in knots [default: 10].
        --moored      Moored (anchored) mine.
        --drifting    Drifting mine.

      ";

          private static void Main(string[] args)
          {
              var arguments = new Docopt().Apply(usage, args, version: "Naval Fate 2.0", exit: true);
              foreach (var argument in arguments)
              {
                  Console.WriteLine("{0} = {1}", argument.Key, argument.Value);
              }
          }
      }
  }

Beat that! The option parser is generated based on the docstring above
that is passed to the ``Docopt.Apply`` method.  ``Docopt.Apply`` parses the usage
pattern (``"Usage: ..."``) and option descriptions (lines starting
with dash "``-``") and ensures that the program invocation matches the
usage pattern; it parses options, arguments and commands based on
that. The basic idea is that *a good help message has all necessary
information in it to make a parser*.

Differences from reference python implementation
======================================================================
- This port should be fully Docopt language compatible with the
  python reference implementation.

- Because C# is statically typed, the return dictionary is of type
  ``IDictionary<string,ValueObject>`` where ``ValueObject`` is a simple wrapper
  class around entry values.

Installation
======================================================================

Use dotnet

    dotnet add package Docopt.net
    
Use nuget

    nuget install Docopt.net

Under Visual Studio
    
    Just drop ``DocoptNet.dll`` file into your project--it is self-contained.

API
======================================================================

.. code:: c#

    using DocoptNet;

.. code:: c#

  public IDictionary<string, ValueObject> Apply(string doc, string cmdLine, bool help = true,
    object version = null, bool optionsFirst = false, bool exit = false);

  public IDictionary<string, ValueObject> Apply(string doc, ICollection<string> argv, bool help = true,
    object version = null, bool optionsFirst = false, bool exit = false);

``Apply`` takes 1 required and 5 optional arguments:

- ``doc`` is a string that contains a **help message** that will be parsed to
  create the option parser.  The simple rules of how to write such a
  help message are given in next sections.  Here is a quick example of
  such a string:

.. code:: c#

    const string USAGE =
    @"Usage: my_program [-hso FILE] [--quiet | --verbose] [INPUT ...]

    -h --help    show this
    -s --sorted  sorted output
    -o FILE      specify output file [default: ./test.txt]
    --quiet      print less text
    --verbose    print more text

    ";

- ``argv`` is an optional argument vector; Alternatively you can supply
   a command line string ``cmdLine``.

- ``help``, by default ``true``, specifies whether the parser should
  automatically print the help message (supplied as ``doc``) and
  terminate, in case ``-h`` or ``--help`` option is encountered
  (options should exist in usage pattern, more on that below). If you
  want to handle ``-h`` or ``--help`` options manually (as other
  options), set ``help: false``.

    Note, you can override the print and exit behaviour by providing
    a custom handler for the ``Docopt.PrintExit`` event. e.g.

.. code:: c#

    var docopt = new Docopt();
    docopt.PrintExit += MyCustomPrintExit;

- ``version``, by default ``null``, is an optional argument that
  specifies the version of your program. If supplied, then, (assuming
  ``--version`` option is mentioned in usage pattern) when parser
  encounters the ``--version`` option, it will print the supplied
  version and terminate.  ``version`` could be any printable object,
  but most likely a string, e.g. ``"2.1.0rc1"``.

    Note, when ``docopt.net`` is set to automatically handle ``-h``,
    ``--help`` and ``--version`` options, you still need to mention
    them in usage pattern for this to work. Also, for your users to
    know about them.

- ``optionsFirst``, by default ``false``.  If set to ``true`` will
  disallow mixing options and positional argument.  I.e. after first
  positional argument, all arguments will be interpreted as positional
  even if they look like options.  This can be used for strict
  compatibility with POSIX, or if you want to dispatch your arguments
  to other programs.

- ``exit``, by default ``false``.  If set to ``false`` will
  raise exceptions based on ``DocoptBaseException`` and will
  not print or exit.
  If set to ``true``  any occurence of ``DocoptBaseException`` will be
  caught by ``Docopt.Apply()``, the error message or the usage will be
  printed, and the program will exit with error code 0 if it's a ``DocoptExitException``, 1 otherwise.

The **return** value is a simple dictionary with options, arguments
and commands as keys, spelled exactly like in your help message.  Long
versions of options are given priority. For example, if you invoke the
top example as::

    naval_fate ship Guardian move 100 150 --speed=15

the return dictionary will be:

.. code:: python

    {'--drifting': False,    'mine': False,
     '--help': False,        'move': True,
     '--moored': False,      'new': False,
     '--speed': '15',        'remove': False,
     '--version': False,     'set': False,
     '<name>': ['Guardian'], 'ship': True,
     '<x>': '100',           'shoot': False,
     '<y>': '150'}

Help message format
======================================================================

Help message consists of 2 parts:

- Usage pattern, e.g.::

    Usage: my_program [-hso FILE] [--quiet | --verbose] [INPUT ...]

- Option descriptions, e.g.::

    -h --help    show this
    -s --sorted  sorted output
    -o FILE      specify output file [default: ./test.txt]
    --quiet      print less text
    --verbose    print more text

Their format is described below; other text is ignored.

Usage pattern format
----------------------------------------------------------------------

**Usage pattern** is a substring of ``doc`` that starts with
``usage:`` (case *insensitive*) and ends with a *visibly* empty line.
Minimum example:

.. code:: c#

    const string USAGE = "Usage: my_program";

The first word after ``usage:`` is interpreted as your program's name.
You can specify your program's name several times to signify several
exclusive patterns:

.. code:: c#

    const string USAGE =
    @"Usage: my_program FILE
             my_program COUNT FILE";

Each pattern can consist of the following elements:

- **<arguments>**, **ARGUMENTS**. Arguments are specified as either
  upper-case words, e.g. ``my_program CONTENT-PATH`` or words
  surrounded by angular brackets: ``my_program <content-path>``.
- **--options**.  Options are words started with dash (``-``), e.g.
  ``--output``, ``-o``.  You can "stack" several of one-letter
  options, e.g. ``-oiv`` which will be the same as ``-o -i -v``. The
  options can have arguments, e.g.  ``--input=FILE`` or ``-i FILE`` or
  even ``-iFILE``. However it is important that you specify option
  descriptions if you want your option to have an argument, a default
  value, or specify synonymous short/long versions of the option (see
  next section on option descriptions).
- **commands** are words that do *not* follow the described above
  conventions of ``--options`` or ``<arguments>`` or ``ARGUMENTS``,
  plus two special commands: dash "``-``" and double dash "``--``"
  (see below).

Use the following constructs to specify patterns:

- **[ ]** (brackets) **optional** elements.  e.g.: ``my_program
  [-hvqo FILE]``
- **( )** (parens) **required** elements.  All elements that are *not*
  put in **[ ]** are also required, e.g.: ``my_program
  --path=<path> <file>...`` is the same as ``my_program
  (--path=<path> <file>...)``.  (Note, "required options" might be not
  a good idea for your users).
- **|** (pipe) **mutually exclusive** elements. Group them using **(
  )** if one of the mutually exclusive elements is required:
  ``my_program (--clockwise | --counter-clockwise) TIME``. Group
  them using **[ ]** if none of the mutually-exclusive elements are
  required: ``my_program [--left | --right]``.
- **...** (ellipsis) **one or more** elements. To specify that
  arbitrary number of repeating elements could be accepted, use
  ellipsis (``...``), e.g.  ``my_program FILE ...`` means one or
  more ``FILE``-s are accepted.  If you want to accept zero or more
  elements, use brackets, e.g.: ``my_program [FILE ...]``. Ellipsis
  works as a unary operator on the expression to the left.
- **[options]** (case sensitive) shortcut for any options.  You can
  use it if you want to specify that the usage pattern could be
  provided with any options defined below in the option-descriptions
  and do not want to enumerate them all in usage-pattern.
- "``[--]``". Double dash "``--``" is used by convention to separate
  positional arguments that can be mistaken for options. In order to
  support this convention add "``[--]``" to your usage patterns.
- "``[-]``". Single dash "``-``" is used by convention to signify that
  ``stdin`` is used instead of a file. To support this add "``[-]``"
  to your usage patterns. "``-``" acts as a normal command.

If your pattern allows to match argument-less option (a flag) several
times::

    Usage: my_program [-v | -vv | -vvv]

then number of occurrences of the option will be counted. I.e.
``args['-v']`` will be ``2`` if program was invoked as ``my_program
-vv``. Same works for commands.

If your usage patterns allows to match same-named option with argument
or positional argument several times, the matched arguments will be
collected into a list::

    Usage: my_program <file> <file> --path=<path>...

I.e. invoked with ``my_program file1 file2 --path=./here
--path=./there`` the returned dict will contain ``args['<file>'] ==
['file1', 'file2']`` and ``args['--path'] == ['./here', './there']``.

Option descriptions format
----------------------------------------------------------------------

**Option descriptions** consist of a list of options that you put
below your usage patterns.

It is necessary to list option descriptions in order to specify:

- synonymous short and long options,
- if an option has an argument,
- if option's argument has a default value.

The rules are as follows:

- Every line in ``doc`` that starts with ``-`` or ``--`` (not counting
  spaces) is treated as an option description, e.g.::

    Options:
      --verbose   # GOOD
      -o FILE     # GOOD
    Other: --bad  # BAD, line does not start with dash "-"

- To specify that option has an argument, put a word describing that
  argument after space (or equals "``=``" sign) as shown below. Follow
  either <angular-brackets> or UPPER-CASE convention for options'
  arguments.  You can use comma if you want to separate options. In
  the example below, both lines are valid, however you are recommended
  to stick to a single style.::

    -o FILE --output=FILE       # without comma, with "=" sign
    -i <file>, --input <file>   # with comma, without "=" sing

- Use two spaces to separate options with their informal description::

    --verbose More text.   # BAD, will be treated as if verbose option had
                           # an argument "More", so use 2 spaces instead
    -q        Quit.        # GOOD
    -o FILE   Output file. # GOOD
    --stdout  Use stdout.  # GOOD, 2 spaces

- If you want to set a default value for an option with an argument,
  put it into the option-description, in form ``[default:
  <my-default-value>]``::

    --coefficient=K  The K coefficient [default: 2.95]
    --output=FILE    Output file [default: test.txt]
    --directory=DIR  Some directory [default: ./]

- If the option is not repeatable, the value inside ``[default: ...]``
  will be interpreted as string.  If it *is* repeatable, it will be
  splited into a list on whitespace::

    Usage: my_program [--repeatable=<arg> --repeatable=<arg>]
                         [--another-repeatable=<arg>]...
                         [--not-repeatable=<arg>]

    # will be ['./here', './there']
    --repeatable=<arg>          [default: ./here ./there]

    # will be ['./here']
    --another-repeatable=<arg>  [default: ./here]

    # will be './here ./there', because it is not repeatable
    --not-repeatable=<arg>      [default: ./here ./there]

Strongly typed arguments with T4 Macro
======================================================================
Include the `T4DocoptNet.tt*` files in your main project to enable the strongly type arguments
code generation. Then add a file called `Main.usage.txt` to your project. Store the usage document in this file instead
of creating a string constant in your program. When the `T4DocoptNet.tt` macro is applied, a class called `MainArgs` in a file called `T4DocoptNet.cs` will be generated.
It will publish the following members:

 - `USAGE` public string constant that returns the usage document.

 - A set of properties that correspond to the commands, arguments, switches and options defined in the
   usage document.

Note that if you name the usage file `Foo.usage.txt`, the generated class will be called `FooArgs`.

Sample Main.usage.txt
----------------------------------------------------------------------

    Test.

    Usage: prog command ARG FILES... [-o --switch --long=ARG]

Resulting T4DocopNet.cs
----------------------------------------------------------------------

The generated class publishes the different argument items as strongly
typed properties. Its constructor accepts the same parameters than
the `Docopt.Apply` method. The original usage text is published
as a string constant e.g. `MainArgs.USAGE`.

.. code:: c#

  // Generated class for Main.usage.txt
  public class MainArgs
  {
    public const string USAGE = @"Test.

  Usage: prog command ARG FILES... [-o --switch --long=ARG]
  ";
    private readonly IDictionary<string, ValueObject> _args;
    public MainArgs(ICollection<string> argv, bool help = true,
                            object version = null, bool optionsFirst = false, bool exit = false)
    {
      _args = new Docopt().Apply(USAGE, argv, help, version, optionsFirst, exit);
    }

    public IDictionary<string, ValueObject> Args
    {
      get { return _args; }
    }

    public bool CmdCommand { get { return _args["command"].IsTrue; } }
    public string ArgArg { get { return _args["ARG"].ToString(); } }
    public bool OptO { get { return _args["-o"].IsTrue; } }
    public string OptLong { get { return _args["--long"].ToString(); } }
    public bool OptSwitch { get { return _args["--switch"].IsTrue; } }
    public ArrayList ArgFiles { get { return _args["FILES"].AsList; } }

  }

Using the generated code
----------------------------------------------------------------------

.. code:: c#

  class Program
  {

     static void DoStuff(string arg, bool flagO, string longValue)
     {
     // ...
     }

    static void Main(string[] argv)
    {
      // Automatically exit(1) if invalid arguments
      var args = new MainArgs(argv, exit: true);
      if (args.CmdCommand)
      {
        Console.WriteLine("First command");
        DoStuff(args.ArgArg, args.OptO, args.OptLong);
      }
    }
  }

You can also access the `MainArgs.USAGE` string constant as follows:
.. code:: c#

  Console.WriteLine("Usage: " + MainArgs.USAGE)

Getting rid of the T4 macro
----------------------------------------------------------------------

If you don't want to use the strongly typed arguments, just delete the
`T4DocoptNet.*` files from the project.

Changelog
======================================================================

**docopt.net** follows `semantic versioning <http://semver.org>`_.  The
first release with stable API will be 1.0.0 (soon).  Until then, you
are encouraged to specify explicitly the version in your dependency
tools, e.g.::

    nuget install docopt.net -Version 0.6.1.11

- 0.6.1.11 Bug fix.
- 0.6.1.8 Added support for .net core RC2.
- 0.6.1.6 Double creation of property bug fix. T4DocoptNet.tt assembly path fix.
- 0.6.1.5 Added strongly typed arguments through T4 macro. ValueObject interface cleanup. exit:true parameter behavior fix.
- 0.6.1.4 Clarified exit parameter behaviour.
- 0.6.1.3 Added exit parameter.
- 0.6.1.2 Fixed docopt capitalisation.
- 0.6.1.1 Initial port. All reference language agnostic tests pass.
