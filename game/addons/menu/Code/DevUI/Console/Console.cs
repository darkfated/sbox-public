namespace Sandbox.UI.Dev;

[Library( "console" )]
public class Console : Panel
{
	static readonly char[] LineSeparators = new[] { '\n', '\r' };

	internal List<LogEvent> Entries = new();
	internal VirtualList Output;
	internal ConsoleTextEntry Input;
	internal TextEntry Filter;
	readonly List<object> filteredEntries = new();

	LogEventPanel logEventPanel;

	struct MessageCategory
	{
		public Button Button;
		public int Count;
		public bool Disabled;

		public void Toggle()
		{
			Disabled = !Disabled;
			Button.SetClass( "disabled", Disabled );
		}

		public void Clear()
		{
			Button.Text = "0";
			Count = 0;
		}
	}

	MessageCategory Message;
	MessageCategory Warning;
	MessageCategory Error;

	public Console()
	{
		Output = AddChild<VirtualList>();
		Output.AddClass( "console_output" );
		Output.ItemHeight = 20;
		Output.OverscanCount = 2;
		Output.PreferScrollToBottom = true;
		Output.OnCreateCell = ( Panel cell, object data ) =>
		{
			var entry = new ConsoleRow();
			entry.Parent = cell;
			entry.SetLogEvent( (LogEvent)data );
			entry.OnEntryClicked = logEventPanel.Switch;
		};

		logEventPanel = AddChild<LogEventPanel>();

		var toolbar = Add.Panel( "toolbar" );
		{
			Input = toolbar.AddChild<ConsoleTextEntry>();
			Input.AddEventListener( "onsubmit", OnSubmit );
			Input.Placeholder = "Run command";
			Input.SuggestionProvider = Input.FillAutoComplete;
			Input.HistoryCookie = "console-input-history";

			Filter = toolbar.AddChild<TextEntry>();
			Filter.AddClass( "filter" );
			Filter.AddEventListener( "onchange", OnFilter );
			Filter.Placeholder = "Filter..";

			Error.Button = toolbar.AddChild( new Button( "0", null, "type err", null ) );
			Error.Button.AddEventListener( "onclick", () => { Error.Toggle(); OnFilter(); } );

			Warning.Button = toolbar.AddChild( new Button( "0", null, "type wrn", null ) );
			Warning.Button.AddEventListener( "onclick", () => { Warning.Toggle(); OnFilter(); } );

			Message.Button = toolbar.AddChild( new Button( "0", null, "type msg", null ) );
			Message.Button.AddEventListener( "onclick", () => { Message.Toggle(); OnFilter(); } );

			toolbar.AddChild( new Button( "logs", () => OpenLogsFolder() ) );
			toolbar.AddChild( new Button( "clear", () => OnClear() ) );
			var scrollConsole = toolbar.AddChild( new Button( null, "arrow_downward", () =>
			{
				Output?.TryScrollToBottom();
			} ) );
			scrollConsole.AddClass( "scroll-down-btn" );
		}

		MenuUtility.AddLogger( OnConsoleMessage );

		Output.AcceptsFocus = true;
		Output.AllowChildSelection = true;

	}

	private void OnConsoleMessage( LogEvent e )
	{
		var message = e.Message;
		if ( string.IsNullOrEmpty( message ) || message.IndexOfAny( LineSeparators ) < 0 )
		{
			AddEvent( e );
			return;
		}

		foreach ( var line in message.Split( LineSeparators, StringSplitOptions.RemoveEmptyEntries ) )
		{
			var split = e;
			split.Message = line;
			AddEvent( split );
		}
	}

	void AddEvent( LogEvent e )
	{
		Entries.Add( e );
		IncrementCategory( e.Level );

		if ( ShouldShowEvent( e ) )
		{
			Output.AddItem( e );
		}
	}

	void OnFilter()
	{
		filteredEntries.Clear();

		foreach ( var entry in Entries )
		{
			if ( ShouldShowEvent( entry ) )
				filteredEntries.Add( entry );
		}

		Output.SetItems( filteredEntries );
	}

	bool ShouldShowEvent( LogEvent e )
	{
		if ( e.Level == LogLevel.Error && Error.Disabled ) return false;
		if ( e.Level == LogLevel.Warn && Warning.Disabled ) return false;
		if ( e.Level == LogLevel.Info && Message.Disabled ) return false;
		if ( e.Level == LogLevel.Trace && Message.Disabled ) return false;

		if ( string.IsNullOrWhiteSpace( Filter.Text ) )
			return true;

		return e.Message.Contains( Filter.Text, StringComparison.OrdinalIgnoreCase );
	}

	void OnClear()
	{
		Sound.Play( "ui.button.press" );
		Output.Clear();
		Entries.Clear();
		logEventPanel?.Clear();

		Message.Clear();
		Warning.Clear();
		Error.Clear();
	}

	void OpenLogsFolder()
	{
		Sound.Play( "ui.button.press" );
		MenuUtility.OpenFolder( Environment.CurrentDirectory + "/logs/" );
	}

	void OutputLine( string line )
	{
		var e = new LogEvent() { Message = $"> {line}", Level = LogLevel.Info, Logger = "in", Time = DateTime.Now };
		AddEvent( e );
		ConsoleSystem.Run( line );
	}

	void OnSubmit()
	{
		var text = Input.Text;
		if ( string.IsNullOrWhiteSpace( text ) )
		{
			ClearInput();
			return;
		}

		foreach ( var line in text.Split( LineSeparators, StringSplitOptions.RemoveEmptyEntries ) )
		{
			ExecuteSubmittedLine( line );
		}

		Output.TryScrollToBottom();

		Input.AddToHistory( text );
		ClearInput();
		Input.FocusInput();
	}

	void ExecuteSubmittedLine( string line )
	{
		if ( line == "clear" )
		{
			OnClear();
			return;
		}

		OutputLine( line );
	}

	void ClearInput()
	{
		if ( !Input.IsValid() )
			return;

		Input.ClearInput();
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );
		UnselectAllInChildren();
	}

	void IncrementCategory( LogLevel level )
	{
		if ( level == LogLevel.Info || level == LogLevel.Trace )
		{
			Message.Count++;
		}
		else if ( level == LogLevel.Warn )
		{
			Warning.Count++;
		}
		else if ( level == LogLevel.Error )
		{
			Error.Count++;
		}

		RefreshCategoryCounts();
	}

	void RefreshCategoryCounts()
	{
		Message.Button.Text = $"{Message.Count:n0}";
		Warning.Button.Text = $"{Warning.Count:n0}";
		Error.Button.Text = $"{Error.Count:n0}";
	}

}
