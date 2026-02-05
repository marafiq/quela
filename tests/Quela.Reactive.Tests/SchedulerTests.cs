using Xunit;
using FluentAssertions;
using NSubstitute;
using Microsoft.Extensions.Logging;
using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Graph;
using Quela.Reactive.Runtime.Scheduling;
using Quela.Reactive.DSL;

namespace Quela.Reactive.Tests;

public class SchedulerTests
{
    private readonly ExecutionScheduler _scheduler;

    public SchedulerTests()
    {
        var logger = Substitute.For<ILogger<ExecutionScheduler>>();
        _scheduler = new ExecutionScheduler(logger);
    }

    [Fact]
    public void ComputeExecutionPlan_EmptyDirtySet_ReturnsEmptyPlan()
    {
        // Arrange
        var graph = OrchestrationBuilder.Create("test", "Test")
            .Field<string>("a").Default("").Add()
            .Build();

        var dirtyNodes = new HashSet<NodeId>();

        // Act
        var plan = _scheduler.ComputeExecutionPlan(graph, dirtyNodes);

        // Assert
        plan.IsEmpty.Should().BeTrue();
        plan.Waves.Should().BeEmpty();
    }

    [Fact]
    public void ComputeExecutionPlan_SingleNode_SingleWave()
    {
        // Arrange
        var graph = OrchestrationBuilder.Create("test", "Test")
            .Field<string>("a").Default("").Add()
            .Build();

        var dirtyNodes = new HashSet<NodeId> { NodeId.Create("a") };

        // Act
        var plan = _scheduler.ComputeExecutionPlan(graph, dirtyNodes);

        // Assert
        plan.Waves.Should().HaveCount(1);
        plan.Waves[0].Nodes.Should().Contain(NodeId.Create("a"));
    }

    [Fact]
    public void ComputeExecutionPlan_DependencyChain_CorrectWaveOrdering()
    {
        // Arrange
        var graph = OrchestrationBuilder.Create("test", "Test")
            .Field<int>("a").Default(0).Add()
            .Computed<int>("b")
                .DependsOn("a")
                .Compute(ctx => ctx.GetOrDefault<int>(new NodeId("a"), 0) + 1)
                .Add()
            .Computed<int>("c")
                .DependsOn("b")
                .Compute(ctx => ctx.GetOrDefault<int>(new NodeId("b"), 0) + 1)
                .Add()
            .Build();

        var dirtyNodes = new HashSet<NodeId> { NodeId.Create("a") };

        // Act
        var plan = _scheduler.ComputeExecutionPlan(graph, dirtyNodes);

        // Assert
        plan.Waves.Should().HaveCount(3);
        plan.Waves[0].Nodes.Should().Contain(NodeId.Create("a"));
        plan.Waves[1].Nodes.Should().Contain(NodeId.Create("b"));
        plan.Waves[2].Nodes.Should().Contain(NodeId.Create("c"));
    }

    [Fact]
    public void ComputeExecutionPlan_ParallelNodes_SameWave()
    {
        // Arrange
        var graph = OrchestrationBuilder.Create("test", "Test")
            .Field<int>("root").Default(0).Add()
            .Computed<int>("branch1")
                .DependsOn("root")
                .Compute(ctx => 1)
                .Add()
            .Computed<int>("branch2")
                .DependsOn("root")
                .Compute(ctx => 2)
                .Add()
            .Build();

        var dirtyNodes = new HashSet<NodeId> { NodeId.Create("root") };

        // Act
        var plan = _scheduler.ComputeExecutionPlan(graph, dirtyNodes);

        // Assert
        plan.Waves.Should().HaveCount(2);

        // root in first wave
        plan.Waves[0].Nodes.Should().Contain(NodeId.Create("root"));

        // branch1 and branch2 should be in the same wave (parallel execution)
        plan.Waves[1].Nodes.Should().Contain(NodeId.Create("branch1"));
        plan.Waves[1].Nodes.Should().Contain(NodeId.Create("branch2"));
    }

    [Fact]
    public void ComputeExecutionPlan_DiamondDependency_CorrectOrdering()
    {
        // Arrange: Diamond pattern
        //      a
        //     / \
        //    b   c
        //     \ /
        //      d
        var graph = OrchestrationBuilder.Create("test", "Test")
            .Field<int>("a").Default(0).Add()
            .Computed<int>("b")
                .DependsOn("a")
                .Compute(ctx => 1)
                .Add()
            .Computed<int>("c")
                .DependsOn("a")
                .Compute(ctx => 2)
                .Add()
            .Computed<int>("d")
                .DependsOn("b", "c")
                .Compute(ctx => 3)
                .Add()
            .Build();

        var dirtyNodes = new HashSet<NodeId> { NodeId.Create("a") };

        // Act
        var plan = _scheduler.ComputeExecutionPlan(graph, dirtyNodes);

        // Assert
        plan.Waves.Should().HaveCount(3);

        // Wave 0: a
        plan.Waves[0].Nodes.Should().Contain(NodeId.Create("a"));

        // Wave 1: b and c (parallel)
        plan.Waves[1].Nodes.Should().HaveCount(2);
        plan.Waves[1].Nodes.Should().Contain(NodeId.Create("b"));
        plan.Waves[1].Nodes.Should().Contain(NodeId.Create("c"));

        // Wave 2: d (depends on both b and c)
        plan.Waves[2].Nodes.Should().Contain(NodeId.Create("d"));
    }
}
