namespace Sandbox.UI.Dev;

[Library( "console" )]
public class Console : Panel
{
	internal List<LogEvent> Entries = new();
	internal VirtualList Output;
	internal TextEntry Input;
	internal TextEntry Filter;
	internal Button ScrollConsole;

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
		Output.ItemHeight = 18;
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
			Input = toolbar.AddChild<TextEntry>();
			Input.AddClass( "input" );
			Input.AddEventListener( "onsubmit", OnSubmit );
			Input.Placeholder = "Run command";
			Input.AutoComplete = FillAutoComplete;
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
			ScrollConsole = toolbar.AddChild( new Button( null, "last_page", () => Output?.TryScrollToBottom() ) );
		}

		MenuUtility.AddLogger( OnConsoleMessage );

		Output.AcceptsFocus = true;
		Output.AllowChildSelection = true;

	}

	private void OnConsoleMessage( LogEvent e )
	{
		if ( e.Message.Contains( '\n' ) || e.Message.Contains( '\r' ) )
		{
			var parts = e.Message.Split( new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries );
			foreach ( var part in parts )
			{
				var ee = e;
				ee.Message = part;

				AddEvent( ee );
			}
		}
		else
		{
			AddEvent( e );
		}
	}

	void AddEvent( LogEvent e )
	{
		Entries.Add( e );

		if ( ShouldShowEvent( e ) )
		{
			Output.AddItem( e );
		}

		if ( e.Level == LogLevel.Info || e.Level == LogLevel.Trace )
		{
			Message.Count++;
			Message.Button.Text = $"{Message.Count:n0}";
		}

		if ( e.Level == LogLevel.Warn )
		{
			Warning.Count++;
			Warning.Button.Text = $"{Warning.Count:n0}";
		}

		if ( e.Level == LogLevel.Error )
		{
			Error.Count++;
			Error.Button.Text = $"{Error.Count:n0}";
		}
	}

	void OnFilter()
	{
		Output.SetItems( Entries.Where( x => ShouldShowEvent( x ) ).Select( x => x as object ) );
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
		Output.Clear();
		Entries.Clear();

		Message.Clear();
		Warning.Clear();
		Error.Clear();
	}

	void OpenLogsFolder()
	{
		MenuUtility.OpenFolder( Environment.CurrentDirectory + "/logs/" );
	}

	void OutputLine( string line )
	{
		var e = new LogEvent() { Message = $"> {line}", Level = LogLevel.Info, Logger = "in", Time = DateTime.Now };
		Entries.Add( e );
		Output.AddItem( e );
		ConsoleSystem.Run( line );
	}

	void OnSubmit()
	{
		var t = Input.Text;
		if ( string.IsNullOrWhiteSpace( t ) )
		{
			ClearInput();
			return;
		}

		if ( t == "clear" )
		{
			OnClear();
		}
		else
		{
			if ( t.Contains( '\n' ) || t.Contains( '\r' ) )
			{
				var parts = t.Split( new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries );
				foreach ( var part in parts )
				{
					OutputLine( part );
				}
			}
			else
			{
				OutputLine( t );
			}
		}

		Output.TryScrollToBottom();

		Input.AddToHistory( t );
		ClearInput();
		Input.Focus();
	}

	void ClearInput()
	{
		if ( !Input.IsValid() )
			return;

		Input.Text = string.Empty;
		Input.CaretPosition = 0;
		Input.DestroyAutoComplete();
		Input.OnValueChanged();
		Input.StateHasChanged();
	}

	private object[] FillAutoComplete( string arg )
	{
		if ( string.IsNullOrWhiteSpace( arg ) )
			return Array.Empty<string>();

		var entries = MenuUtility.AutoComplete( arg, 20 ).ToList();
		entries = entries
			.OrderByDescending( x => string.Equals( x.Command, arg, StringComparison.OrdinalIgnoreCase ) )
			.ThenByDescending( x => x.Command.StartsWith( arg, StringComparison.OrdinalIgnoreCase ) )
			.ThenBy( x => x.Command )
			.ToList();

		return entries.Select( x => (object)new TextEntry.AutocompleteEntry
		{
			Title = x.Command,
			Value = x.Command
		} ).ToArray();
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );

		foreach ( var child in Children )
		{
			Unselect( child );
		}
	}

	public override void Tick()
	{
		base.Tick();
		ScrollConsole?.SetClass( "active", Output?.IsScrollAtBottom ?? false );
		Input?.SetClass( "empty", string.IsNullOrEmpty( Input.Text ) );
		Filter?.SetClass( "empty", string.IsNullOrEmpty( Filter.Text ) );
	}

	private void Unselect( Panel p )
	{
		if ( p is Label l )
		{
			l.ShouldDrawSelection = false;
			return;
		}

		foreach ( var child in p.Children )
		{
			Unselect( child );
		}
	}

}
