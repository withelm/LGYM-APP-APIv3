using System.Reflection;
using System.Diagnostics;
using LgymApi.Application.Mapping.Core;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class MapperNestedCompositionTests
{
    [Test]
    public void Map_Should_Support_Nested_Object_And_List_Mapping_With_Context_Propagation()
    {
        var mapper = CreateMapper(new NestedCompositionProfile());
        var context = mapper.CreateContext();
        context.Set(NestedCompositionProfile.Keys.Suffix, "!");

        var source = new ParentSource
        {
            Child = new ChildSource { Name = "bench" },
            Children =
            [
                new ChildSource { Name = "squat" },
                new ChildSource { Name = "deadlift" }
            ]
        };

        var result = mapper.Map<ParentSource, ParentTarget>(source, context);

        Assert.Multiple(() =>
        {
            Assert.That(result.Child?.Name, Is.EqualTo("bench!"));
            Assert.That(result.Children.Select(c => c.Name), Is.EqualTo(new[] { "squat!", "deadlift!" }));
        });
    }

    [Test]
    public void Map_Should_Throw_Clear_Error_When_Nested_Mapping_Is_Missing()
    {
        var mapper = CreateMapper(new MissingNestedMappingProfile());
        var source = new MissingParentSource { Child = new MissingChildSource() };

        var ex = Assert.Throws<InvalidOperationException>(() => mapper.Map<MissingParentSource, MissingParentTarget>(source));

        Assert.That(ex!.Message, Does.Contain("Mapping from MissingChildSource to MissingChildTarget is not registered."));
    }

    [Test]
    public void Map_Should_Throw_Clear_Error_When_Cyclic_Nested_Mapping_Is_Detected()
    {
        var mapper = CreateMapper(new CyclicNestedMappingProfile());
        var source = new CyclicSource { TriggerCycle = true };

        var ex = Assert.Throws<InvalidOperationException>(() => mapper.Map<CyclicSource, CyclicTarget>(source));

        Assert.That(ex!.Message, Does.Contain("Cyclic nested mapping detected."));
    }

    [Test]
    public void Map_Should_Throw_Clear_Error_When_Cyclic_ValueType_Nested_Mapping_Is_Detected()
    {
        var mapper = CreateMapper(new CyclicValueTypeMappingProfile());

        var ex = Assert.Throws<InvalidOperationException>(() => mapper.Map<int, int>(1));

        Assert.That(ex!.Message, Does.Contain("Cyclic nested mapping detected."));
    }

    [Test]
    public void Map_Should_Throw_When_Context_Is_Bound_To_Different_Mapper()
    {
        var firstMapper = CreateMapper(new NestedCompositionProfile());
        var secondMapper = CreateMapper(new NestedCompositionProfile());
        var context = firstMapper.CreateContext();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            secondMapper.Map<ParentSource, ParentTarget>(new ParentSource(), context));

        Assert.That(ex!.Message, Does.Contain("already bound to a different mapper"));
    }

    [Test]
    public async Task Map_Should_Not_Leak_Cycle_State_Between_Parallel_Executions()
    {
        var mapper = CreateMapper(new ParallelValueTypeMappingProfile());
        var context = mapper.CreateContext();

        var first = Task.Run(() => mapper.Map<int, int>(1, context));
        var second = Task.Run(() => mapper.Map<int, int>(1, context));

        var results = await Task.WhenAll(first, second);

        Assert.That(results, Is.EqualTo(new[] { 1, 1 }));
    }

    private static IMapper CreateMapper(params IMappingProfile[] profiles)
    {
        return (IMapper)Activator.CreateInstance(
            typeof(Mapper),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [profiles],
            culture: null)!;
    }

    private sealed class NestedCompositionProfile : IMappingProfile
    {
        internal static class Keys
        {
            internal static readonly ContextKey<string> Suffix = new("Tests.Suffix");
        }

        public void Configure(MappingConfiguration configuration)
        {
            configuration.AllowContextKey(Keys.Suffix);

            configuration.CreateMap<ChildSource, ChildTarget>((source, context) =>
            {
                var suffix = context?.Get(Keys.Suffix) ?? string.Empty;
                return new ChildTarget { Name = source.Name + suffix };
            });

            configuration.CreateMap<ParentSource, ParentTarget>((source, context) => new ParentTarget
            {
                Child = source.Child == null ? null : context!.Map<ChildSource, ChildTarget>(source.Child),
                Children = context!.MapList<ChildSource, ChildTarget>(source.Children)
            });
        }
    }

    private sealed class MissingNestedMappingProfile : IMappingProfile
    {
        public void Configure(MappingConfiguration configuration)
        {
            configuration.CreateMap<MissingParentSource, MissingParentTarget>((source, context) => new MissingParentTarget
            {
                Child = source.Child == null ? null : context!.Map<MissingChildSource, MissingChildTarget>(source.Child)
            });
        }
    }

    private sealed class CyclicNestedMappingProfile : IMappingProfile
    {
        public void Configure(MappingConfiguration configuration)
        {
            configuration.CreateMap<CyclicSource, CyclicTarget>((source, context) =>
            {
                if (source.TriggerCycle)
                {
                    return context!.Map<CyclicSource, CyclicTarget>(source);
                }

                return new CyclicTarget();
            });
        }
    }

    private sealed class CyclicValueTypeMappingProfile : IMappingProfile
    {
        public void Configure(MappingConfiguration configuration)
        {
            configuration.CreateMap<int, int>((source, context) =>
            {
                if (source == 1)
                {
                    return context!.Map<int, int>(source);
                }

                return source;
            });
        }
    }

    private sealed class ParallelValueTypeMappingProfile : IMappingProfile
    {
        private int _started;

        public void Configure(MappingConfiguration configuration)
        {
            configuration.CreateMap<int, int>((source, _) =>
            {
                Interlocked.Increment(ref _started);

                var wait = Stopwatch.StartNew();
                while (Volatile.Read(ref _started) < 2 && wait.Elapsed < TimeSpan.FromMilliseconds(250))
                {
                    Thread.SpinWait(1000);
                }

                return source;
            });
        }
    }

    private sealed class ParentSource
    {
        public ChildSource? Child { get; init; }
        public List<ChildSource> Children { get; init; } = [];
    }

    private sealed class ChildSource
    {
        public string Name { get; init; } = string.Empty;
    }

    private sealed class ParentTarget
    {
        public ChildTarget? Child { get; init; }
        public List<ChildTarget> Children { get; init; } = [];
    }

    private sealed class ChildTarget
    {
        public string Name { get; init; } = string.Empty;
    }

    private sealed class MissingParentSource
    {
        public MissingChildSource? Child { get; init; }
    }

    private sealed class MissingChildSource;

    private sealed class MissingParentTarget
    {
        public MissingChildTarget? Child { get; init; }
    }

    private sealed class MissingChildTarget;

    private sealed class CyclicSource
    {
        public bool TriggerCycle { get; init; }
    }

    private sealed class CyclicTarget;
}
