namespace Sandbox.UI.Dev;

public class ConsoleInput : TextEntry
{
	public SuggestionPanel SuggestionsPanel { get; set; }
	public Func<string, object[]> SuggestionProvider { get; set; }

	public override void OnButtonEvent( ButtonEvent e )
	{
		if ( e.Button == "tab" && SuggestionsPanel?.HasItems == true )
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
		SuggestionsPanel?.Hide();
	}

	public void FocusInput() => Focus();

	public void ClearInput()
	{
		Text = string.Empty;
		CaretPosition = 0;
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
		if ( refreshSuggestions )
			OnValueChanged();
		Focus();
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
