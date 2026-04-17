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
		var content = Add.Panel( "content" );
		var header = content.Add.Panel( "header" );

		var symbol = header.Add.Icon( "error" );
		symbol.AddClass( "symbol" );
		header.Add.Label( "Code Error", "title" );

		message = content.Add.Label( "Something went wrong! This is an exception notice!", "message" );
		traceContainer = content.AddChild<NoWheelPanel>();
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
}
