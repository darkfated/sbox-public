using System.Linq;

namespace Sandbox.UI.Dev;

public class ConsoleOverlay : Panel
{
	[MenuConVar( "consoleoverlay", Help = "Enable the console to draw at the top of the screen all the time", Saved = true )]
	public static bool ConsoleOverlayEnabled { get; set; }

	internal Panel Output;

	public ConsoleOverlay()
	{
		Output = Add.Panel( "output" );
		MenuUtility.AddLogger( OnConsoleMessage );
	}

	private void OnConsoleMessage( LogEvent e )
	{
		if ( !ConsoleOverlayEnabled )
			return;

		var entry = Output.AddChild<ConsoleEntry>();
		entry.SetLogEvent( e );
		entry.DeleteIn( 8 );

		while ( Output.Children.Count() > 10 )
		{
			Output.Children.First().Delete( true );
		}
	}
}
