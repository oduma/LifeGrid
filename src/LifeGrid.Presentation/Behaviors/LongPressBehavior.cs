using System.Windows.Input;

namespace LifeGrid.Presentation.Behaviors;

public sealed class LongPressBehavior : Behavior<View>
{
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(LongPressBehavior));

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(LongPressBehavior));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    protected override void OnAttachedTo(View bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.HandlerChanged += OnHandlerChanged;
        // Inside a CollectionView the handler is often already connected by the time
        // the behavior is applied — register immediately rather than waiting for the event.
        RegisterNativeLongClick(bindable);
    }

    protected override void OnDetachingFrom(View bindable)
    {
        bindable.HandlerChanged -= OnHandlerChanged;
        UnregisterNativeLongClick(bindable);
        base.OnDetachingFrom(bindable);
    }

    private void OnHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is View v)
            RegisterNativeLongClick(v);
    }

    private void RegisterNativeLongClick(View v)
    {
#if ANDROID
        if (v.Handler?.PlatformView is Android.Views.View nativeView)
        {
            // Unsubscribe first to prevent double-subscription on handler reconnects.
            nativeView.LongClick -= OnLongClick;
            nativeView.LongClick += OnLongClick;
        }
#endif
    }

    private void UnregisterNativeLongClick(View v)
    {
#if ANDROID
        if (v.Handler?.PlatformView is Android.Views.View nativeView)
            nativeView.LongClick -= OnLongClick;
#endif
    }

#if ANDROID
    private void OnLongClick(object? sender, Android.Views.View.LongClickEventArgs e)
    {
        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
        e.Handled = true;
    }
#endif
}
