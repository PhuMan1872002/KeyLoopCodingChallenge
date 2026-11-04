using Microsoft.Extensions.Logging.Abstractions;
using Keyloop.Inventory.Application.Queries;
using Keyloop.Inventory.Domain.Entities;
using Keyloop.Inventory.Domain.Enums;
using Keyloop.Inventory.Application.Dtos;
using Keyloop.Inventory.Infrastructure.Services;
using Keyloop.Inventory.Tests.Domain;
using Keyloop.Inventory.Web.ViewModels;


namespace Keyloop.Inventory.Tests.Infrastructure;

public class ActionLogWorkflowTests
{
    private static readonly Func<Task> Never = () => new TaskCompletionSource().Task;

    private static ActionLogWorkflow Create(
        TestData.FakeInventoryClient client,
        InventoryNotificationService? notifications = null,
        Func<Task>? onExternalChange = null,
        Func<Task>? toastLifetime = null) => new(
            new InventoryQueryService(client),
            notifications ?? new InventoryNotificationService(NullLogger<InventoryNotificationService>.Instance),
            new InventoryTelemetry(),
            NullLogger.Instance,
            onExternalChange ?? (() => Task.CompletedTask),
            toastLifetime ?? Never);

    [Fact]
    public async Task Saving_persists_the_action_and_sets_a_toast_naming_it()
    {
        var client = new TestData.FakeInventoryClient(TestData.Vehicle("VIN-1"));
        using var workflow = Create(client, toastLifetime: Never);

        await workflow.SaveAsync(
            "VIN-1",
            "ST-VIN-1",
            TestData.Action(ActionStatus.PriceReductionPlanned, new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero)));

        var vehicles = await client.GetAllAsync();
        Assert.Equal(ActionStatus.PriceReductionPlanned, Assert.Single(vehicles).CurrentAction?.Status);
        Assert.Equal("Price reduction planned saved for ST-VIN-1", workflow.Toast);
    }

    [Fact]
    public async Task Saving_against_an_unknown_vin_sets_an_explanatory_toast_instead_of_throwing()
    {
        using var workflow = Create(new TestData.FakeInventoryClient(), toastLifetime: Never);

        await workflow.SaveAsync(
            "VIN-GONE", "ST-GONE", TestData.Action(ActionStatus.PriceReductionPlanned, DateTimeOffset.UnixEpoch));

        Assert.Equal("That vehicle is no longer in inventory.", workflow.Toast);
    }

    [Fact]
    public async Task The_toast_clears_itself_once_its_lifetime_is_up()
    {
        var expired = new Queue<TaskCompletionSource>();
        using var workflow = Create(
            new TestData.FakeInventoryClient(TestData.Vehicle("VIN-1")),
            toastLifetime: () => Enqueue(expired));

        await workflow.SaveAsync("VIN-1", "ST-1", TestData.Action(ActionStatus.MarketingPromotion));
        Assert.NotNull(workflow.Toast);

        expired.Dequeue().SetResult();
        await workflow.PendingToastDismissal;

        Assert.Null(workflow.Toast);
    }

    [Fact]
    public async Task A_second_save_before_the_first_toast_clears_keeps_the_newer_message()
    {
        var expired = new Queue<TaskCompletionSource>();
        using var workflow = Create(
            new TestData.FakeInventoryClient(TestData.Vehicle("VIN-1"), TestData.Vehicle("VIN-2")),
            toastLifetime: () => Enqueue(expired));

        await workflow.SaveAsync("VIN-1", "ST-1", TestData.Action(ActionStatus.MarketingPromotion));
        var firstDismissal = workflow.PendingToastDismissal;

        await workflow.SaveAsync("VIN-2", "ST-2", TestData.Action(ActionStatus.Reconditioning));
        var secondDismissal = workflow.PendingToastDismissal;

        expired.Dequeue().SetResult();
        await firstDismissal;
        Assert.Equal("Reconditioning / detail saved for ST-2", workflow.Toast);

        expired.Dequeue().SetResult();
        await secondDismissal;
        Assert.Null(workflow.Toast);
    }

    private static Task Enqueue(Queue<TaskCompletionSource> expiries)
    {
        var expiry = new TaskCompletionSource();
        expiries.Enqueue(expiry);
        return expiry.Task;
    }

    [Fact]
    public async Task Each_workflow_has_its_own_circuit_id_and_ignores_its_own_broadcast()
    {
        var notifications = new InventoryNotificationService(NullLogger<InventoryNotificationService>.Instance);
        var externalCalls = 0;

        using var workflow = Create(
            new TestData.FakeInventoryClient(TestData.Vehicle("VIN-1")),
            notifications,
            onExternalChange: () => { externalCalls++; return Task.CompletedTask; },
            toastLifetime: Never);

        await workflow.SaveAsync("VIN-1", "ST-1", TestData.Action(ActionStatus.MarketingPromotion, DateTimeOffset.UnixEpoch));

        Assert.Equal(0, externalCalls);

        await notifications.PublishAsync(new VehicleChanged("VIN-1", "some-other-circuit"));
        Assert.Equal(1, externalCalls);
    }

    [Fact]
    public async Task A_broadcast_failure_in_one_subscriber_does_not_stop_the_others()
    {
        var notifications = new InventoryNotificationService(NullLogger<InventoryNotificationService>.Instance);
        var reached = new List<string>();

        using var failing = notifications.Subscribe(_ => throw new InvalidOperationException("circuit torn down oddly"));
        using var healthy = notifications.Subscribe(change => { reached.Add(change.Vin); return Task.CompletedTask; });

        using var workflow = Create(new TestData.FakeInventoryClient(TestData.Vehicle("VIN-1")), notifications,
            toastLifetime: Never);

        await workflow.SaveAsync("VIN-1", "ST-1", TestData.Action(ActionStatus.MarketingPromotion, DateTimeOffset.UnixEpoch));

        Assert.Equal(["VIN-1"], reached);
    }
}
