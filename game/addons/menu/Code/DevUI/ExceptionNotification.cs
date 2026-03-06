using Sandbox.UI.Construct;

namespace Sandbox.UI.Dev;

public class ExceptionNotification : Panel
{
	class NoWheelPanel : Panel
	{
		public override void OnMouseWheel( Vector2 value )
		{
			// Disable wheel scrolling to match console behavior.
		}
	}

	Label message;
	Label trace;
	Panel traceContainer;
	RealTimeSince TimeSinceLastError;

	public ExceptionNotification()
	{
		Add.Icon( "😥" );

		var column = new Panel( this, "column" );
		column.AddChild( new Label() { Text = "Code Error" } );

		message = column.Add.Label( "Something went wrong! This is an exception notice!", "message" );
		traceContainer = column.AddChild<NoWheelPanel>();
		traceContainer.AddClass( "trace-container" );
		trace = traceContainer.Add.Label( "", "trace" );
		trace.Multiline = true;
		trace.Selectable = true;
		SetClass( "hidden", true );
		TimeSinceLastError = 100;
	}

	public override void Tick()
	{
		base.Tick();

		SetClass( "hidden", TimeSinceLastError > 8 );
		SetClass( "fresh", TimeSinceLastError < 0.2f );

		PositionLikeAutocomplete();
	}

	internal void OnException( LogEvent entry )
	{
		message.Text = entry.Message?.Split( '\n', System.StringSplitOptions.RemoveEmptyEntries ).FirstOrDefault()?.Trim() ?? "null";
		trace.Text = string.IsNullOrWhiteSpace( entry.Stack ) ? entry.Message ?? string.Empty : entry.Stack;
		SetClass( "has-trace", !string.IsNullOrWhiteSpace( trace.Text ) );
		TimeSinceLastError = 0;
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );
		e.StopPropagation();
	}

	protected override void OnMouseMove( MousePanelEvent e )
	{
		base.OnMouseMove( e );
		e.StopPropagation();
	}

	protected override void OnMouseUp( MousePanelEvent e )
	{
		base.OnMouseUp( e );
		e.StopPropagation();
	}

	public override void OnMouseWheel( Vector2 value )
	{
		// Swallow wheel input so scrolling this panel doesn't scroll UI behind it.
	}

	void PositionLikeAutocomplete()
	{
		var input = DeveloperMode.Current?.Console?.Input;
		if ( !input.IsValid() )
			return;

		var inputRect = input.Box.Rect * input.ScaleFromScreen;
		var height = (Box.Rect.Height * ScaleFromScreen);
		if ( height <= 0 )
			height = 260f;

		var top = inputRect.Top - 8f - height;
		top = top.Clamp( 16f, Screen.Height - height - 16f );

		Style.Top = top;
		Style.Bottom = null;
		Style.Dirty();
	}
}
