using Sandbox.UI.Construct;

namespace Sandbox.UI.Dev;

public class ExceptionNotification : Panel
{
	private readonly Label message;
	private readonly Label trace;
	private readonly Panel traceContainer;
	private RealTimeSince timeSinceLastError;

	public ExceptionNotification()
	{
		var content = Add.Panel( "content" );
		var header = content.Add.Panel( "header" );

		var symbol = header.Add.Icon( "error" );
		symbol.AddClass( "symbol" );
		header.Add.Label( "Code Error", "title" );

		message = content.Add.Label( "Something went wrong! This is an exception notice!", "message" );
		traceContainer = content.Add.Panel( "trace-container" );
		trace = traceContainer.Add.Label( "", "trace" );
		trace.Multiline = true;
		trace.Selectable = true;
		SetClass( "hidden", true );
		timeSinceLastError = 100;
	}

	public override void Tick()
	{
		base.Tick();

		SetClass( "hidden", timeSinceLastError > 8 );
		SetClass( "fresh", timeSinceLastError < 0.2f );
	}

	internal void OnException( LogEvent entry )
	{
		var lines = entry.Message?.Split( '\n', System.StringSplitOptions.RemoveEmptyEntries );
		message.Text = lines is { Length: > 0 } ? lines[0].Trim() : "null";
		trace.Text = string.IsNullOrWhiteSpace( entry.Stack ) ? entry.Message ?? string.Empty : entry.Stack;
		SetClass( "has-trace", !string.IsNullOrWhiteSpace( trace.Text ) );
		timeSinceLastError = 0;
	}
}
