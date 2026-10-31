using Microsoft.Xaml.Behaviors;
using System.Windows.Controls;
using TinyHex.Behaviors;
using TinyHex.Models;

namespace TinyHex.Tests.Behaviors;

[TestFixture]
public class ScrollIntoViewBehaviorTests
{
    private static bool HasSubscribers(ScrollRequestNotifier notifier)
    {
        var field = typeof(ScrollRequestNotifier).GetField(nameof(ScrollRequestNotifier.ScrollRequested),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return field!.GetValue(notifier) != null;
    }

    [Test]
    public void Attach_SubscribesToNotifier_AndRequestWithoutVisualTreeDoesNotThrow()
    {
        var listBox = new ListBox();
        var notifier = new ScrollRequestNotifier();
        var behavior = new ScrollIntoViewBehavior { Notifier = notifier };

        Interaction.GetBehaviors(listBox).Add(behavior);

        Assert.That(HasSubscribers(notifier), Is.True);
        Assert.DoesNotThrow(() => notifier.Request(5));
    }

    [Test]
    public void Detach_UnsubscribesFromNotifier()
    {
        var listBox = new ListBox();
        var notifier = new ScrollRequestNotifier();
        var behavior = new ScrollIntoViewBehavior { Notifier = notifier };
        Interaction.GetBehaviors(listBox).Add(behavior);

        Interaction.GetBehaviors(listBox).Remove(behavior);

        Assert.That(HasSubscribers(notifier), Is.False);
    }

    [Test]
    public void ReplacingNotifier_MovesSubscription()
    {
        var listBox = new ListBox();
        var first = new ScrollRequestNotifier();
        var second = new ScrollRequestNotifier();
        var behavior = new ScrollIntoViewBehavior();
        Interaction.GetBehaviors(listBox).Add(behavior);

        behavior.Notifier = first;
        Assert.That(HasSubscribers(first), Is.True);

        behavior.Notifier = second;
        Assert.That(HasSubscribers(first), Is.False);
        Assert.That(HasSubscribers(second), Is.True);
    }
}
