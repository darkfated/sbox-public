namespace Sandbox.UI.Dev;

public class ConsoleInput : TextEntry
{
	public SuggestionPanel SuggestionsPanel { get; set; }
	public Func<string, object[]> SuggestionProvider { get; set; }

	const int MaxHistorySuggestions = 10;
	readonly List<string> commandHistory = new();
	int historyIndex = -1;
	bool historyCycling;

	public override void OnButtonEvent( ButtonEvent e )
	{
		if (e.Button == "tab" && (SuggestionsPanel?.HasItems == true || CanCycleHistory()))
		{
			e.StopPropagation = true;
			return;
		}

		base.OnButtonEvent( e );
	}

	public override void OnButtonTyped( ButtonEvent e )
	{
		if ( e.Button == "tab" )
		{
			if ( CanCycleHistory() )
			{
				e.StopPropagation = true;
				CycleHistory( 1 );
				return;
			}

			if ( SuggestionsPanel?.HasItems == true )
			{
				e.StopPropagation = true;
				SuggestionsPanel.MoveSelection( 1 );
				ApplySuggestion( SuggestionsPanel.GetSelectedValue(), false );
				return;
			}
		}

		if ( e.Button == "up" || e.Button == "down" )
		{
			e.StopPropagation = true;

			if ( SuggestionsPanel?.HasItems == true )
			{
				SuggestionsPanel.MoveSelection( e.Button == "up" ? -1 : 1 );
				ApplySuggestion( SuggestionsPanel.GetSelectedValue(), false );
			}

			return;
		}

		if ( e.Button == "escape" && SuggestionsPanel?.Visible == true )
		{
			e.StopPropagation = true;
			SuggestionsPanel.Hide();
			return;
		}

		base.OnButtonTyped( e );
	}

	public override void OnValueChanged()
	{
		base.OnValueChanged();
		StopHistoryCycling();
		SuggestionsPanel?.Update( Text, SuggestionProvider );
	}

	protected override void OnFocus( PanelEvent e )
	{
		base.OnFocus( e );
		SuggestionsPanel?.Update( Text, SuggestionProvider );
	}

	protected override void OnBlur( PanelEvent e )
	{
		base.OnBlur( e );
		StopHistoryCycling();
		SuggestionsPanel?.Hide();
	}

	public void FocusInput() => Focus();

	public void ClearInput()
	{
		Text = string.Empty;
		CaretPosition = 0;
		StopHistoryCycling();
		SuggestionsPanel?.Hide();
		OnValueChanged();
		StateHasChanged();
	}

	public void ApplySuggestion( string value, bool refreshSuggestions = true )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
			return;

		Text = value;
		CaretPosition = TextLength;
		StopHistoryCycling();
		if ( refreshSuggestions )
			OnValueChanged();
		Focus();
	}

	public void AddCommandHistory( string command )
	{
		if ( string.IsNullOrWhiteSpace( command ) )
			return;

		command = command.Trim();

		commandHistory.RemoveAll( x => x.Equals( command, StringComparison.OrdinalIgnoreCase ) );
		commandHistory.Insert( 0, command );

		while ( commandHistory.Count > MaxHistorySuggestions )
			commandHistory.RemoveAt( commandHistory.Count - 1 );
	}

	bool CanCycleHistory()
	{
		return historyCycling || (string.IsNullOrWhiteSpace(Text) && commandHistory.Count > 0);
	}

	void StopHistoryCycling()
	{
		historyCycling = false;
		historyIndex = -1;
	}

	void CycleHistory( int direction )
	{
		if ( !CanCycleHistory() )
			return;

		if ( !historyCycling )
		{
			historyCycling = true;
			historyIndex = -1;
		}

		historyIndex = (historyIndex + direction + commandHistory.Count) % commandHistory.Count;
		Text = commandHistory[historyIndex];
		CaretPosition = TextLength;
		SuggestionsPanel?.Hide();
		StateHasChanged();
	}

	public object[] FillAutoComplete( string arg )
	{
		if ( string.IsNullOrWhiteSpace( arg ) )
			return Array.Empty<object>();

		var trimmed = arg.TrimStart();
		var firstSeparator = trimmed.IndexOf( ' ' );
		var firstToken = firstSeparator < 0 ? trimmed : trimmed[..firstSeparator];
		var hasSecondArgumentInput = firstSeparator >= 0 && !string.IsNullOrWhiteSpace( trimmed[(firstSeparator + 1)..] );
		var query = hasSecondArgumentInput ? arg : firstToken;

		return MenuUtility.AutoComplete( query, 20 )
			.GroupBy( x => x.Command, StringComparer.OrdinalIgnoreCase )
			.Select( x => x.First() )
			.OrderByDescending( x => string.Equals( x.Command, firstToken, StringComparison.OrdinalIgnoreCase ) )
			.ThenByDescending( x => x.Command.StartsWith( firstToken, StringComparison.OrdinalIgnoreCase ) )
			.ThenBy( x => x.Command )
			.Select( x => (object)new TextEntry.AutocompleteEntry
			{
				Title = x.Command,
				Value = x.Command
			} )
			.ToArray();
	}
}
