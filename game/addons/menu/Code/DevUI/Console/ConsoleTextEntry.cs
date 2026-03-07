namespace Sandbox.UI.Dev;

public class ConsoleTextEntry : TextEntry
{
	struct SuggestionItem
	{
		public string Title;
		public string Value;
		public string Command;
		public string CurrentValue;
	}

	public Func<string, object[]> SuggestionProvider { get; set; }

	Panel suggestionPanel;
	readonly List<SuggestionItem> suggestionItems = new();
	int selectedSuggestionIndex = -1;
	bool preserveSuggestionListOnce;

	public ConsoleTextEntry()
	{
		AddClass( "console-textentry" );
	}

	public override void OnButtonEvent( ButtonEvent e )
	{
		if ( e.Button == "tab" )
		{
			e.StopPropagation = true;
			return;
		}

		base.OnButtonEvent( e );
	}

	public override void OnButtonTyped( ButtonEvent e )
	{
		var button = e.Button;

		if ( button == "tab" )
		{
			if ( e.HasShift )
			{
				e.StopPropagation = true;
				return;
			}

			if ( !suggestionPanel.IsValid() )
				UpdateSuggestionPopup();

			if ( suggestionPanel.IsValid() && suggestionItems.Count > 0 )
			{
				e.StopPropagation = true;
				MoveSuggestionSelection( 1 );
				return;
			}
		}

		if ( button == "up" || button == "down" )
		{
			if ( suggestionPanel.IsValid() && suggestionItems.Count > 0 )
			{
				e.StopPropagation = true;
				MoveSuggestionSelection( button == "up" ? -1 : 1 );
				return;
			}

			e.StopPropagation = true;
			return;
		}

		if ( button == "escape" && suggestionPanel.IsValid() )
		{
			e.StopPropagation = true;
			DestroySuggestionPopup();
			return;
		}

		base.OnButtonTyped( e );
	}

	public override void OnValueChanged()
	{
		base.OnValueChanged();
		UpdateSuggestionPopup();
	}

	protected override void OnFocus( PanelEvent e )
	{
		base.OnFocus( e );
		UpdateSuggestionPopup();
	}

	protected override void OnBlur( PanelEvent e )
	{
		base.OnBlur( e );
		DestroySuggestionPopup();
	}

	public void FocusInput() => Focus();

	public void ClearInput()
	{
		Text = string.Empty;
		CaretPosition = 0;
		DestroySuggestionPopup();
		OnValueChanged();
		StateHasChanged();
	}

	public object[] FillAutoComplete( string arg )
	{
		if ( string.IsNullOrWhiteSpace( arg ) )
			return Array.Empty<string>();

		var trimmed = arg.TrimStart();
		var firstSeparator = trimmed.IndexOf( ' ' );
		var firstToken = firstSeparator < 0 ? trimmed : trimmed[..firstSeparator];
		var hasSecondArgumentInput = firstSeparator >= 0 && !string.IsNullOrWhiteSpace( trimmed[(firstSeparator + 1)..] );
		var query = hasSecondArgumentInput ? arg : firstToken;

		var entries = MenuUtility.AutoComplete( query, 20 )
			.GroupBy( x => x.Command, StringComparer.OrdinalIgnoreCase )
			.Select( x => x.First() )
			.OrderByDescending( x => string.Equals( x.Command, firstToken, StringComparison.OrdinalIgnoreCase ) )
			.ThenByDescending( x => x.Command.StartsWith( firstToken, StringComparison.OrdinalIgnoreCase ) )
			.ThenBy( x => x.Command )
			.ToList();

		return entries.Select( x => (object)new TextEntry.AutocompleteEntry
		{
			Title = x.Command,
			Value = x.Command
		} ).ToArray();
	}

	void UpdateSuggestionPopup()
	{
		if ( SuggestionProvider == null )
		{
			DestroySuggestionPopup();
			return;
		}

		var options = SuggestionProvider( Text ) ?? Array.Empty<object>();
		if ( options.Length == 0 )
		{
			if ( preserveSuggestionListOnce && suggestionPanel.IsValid() && suggestionItems.Count > 0 )
			{
				preserveSuggestionListOnce = false;
				UpdateSelectionClasses();
				UpdateTypedMatchClasses();
				return;
			}

			preserveSuggestionListOnce = false;
			DestroySuggestionPopup();
			return;
		}

		preserveSuggestionListOnce = false;

		if ( !suggestionPanel.IsValid() || suggestionPanel.IsDeleting )
		{
			suggestionPanel = Add.Panel( "console-suggestion-menu" );
		}

		suggestionPanel.DeleteChildren( true );
		suggestionItems.Clear();

		foreach ( var option in options )
		{
			var item = ConvertOption( option );
			if ( string.IsNullOrWhiteSpace( item.Value ) )
				continue;

			item.Command = ExtractFirstToken( item.Value );
			item.CurrentValue = string.IsNullOrWhiteSpace( item.Command ) ? null : ConsoleSystem.GetValue( item.Command );
			suggestionItems.Add( item );

			var suggestionValue = item.Value;
			var row = suggestionPanel.Add.Panel( "suggestion-item" );
			row.AddChild( new Label( item.Title, "name" ) );
			row.AddChild( new Label( item.CurrentValue ?? string.Empty, "value" ) );
			row.AddEventListener( "onclick", () => ApplySuggestion( suggestionValue ) );
			row.UserData = item;
		}

		if ( suggestionItems.Count == 0 )
		{
			DestroySuggestionPopup();
			return;
		}

		selectedSuggestionIndex = suggestionItems.FindIndex( x => string.Equals( x.Value, Text, StringComparison.OrdinalIgnoreCase ) );
		UpdateSelectionClasses();
		UpdateTypedMatchClasses();
	}

	void DestroySuggestionPopup()
	{
		suggestionPanel?.Delete();
		suggestionPanel = null;
		suggestionItems.Clear();
		selectedSuggestionIndex = -1;
	}

	static SuggestionItem ConvertOption( object option )
	{
		if ( option is TextEntry.AutocompleteEntry entry )
		{
			var value = entry.Value?.ToString() ?? string.Empty;
			return new SuggestionItem
			{
				Title = string.IsNullOrWhiteSpace( entry.Title ) ? value : entry.Title,
				Value = value
			};
		}

		var raw = option?.ToString() ?? string.Empty;
		return new SuggestionItem
		{
			Title = raw,
			Value = raw
		};
	}

	void MoveSuggestionSelection( int direction )
	{
		if ( suggestionItems.Count == 0 )
			return;

		if ( selectedSuggestionIndex < 0 )
			selectedSuggestionIndex = direction > 0 ? 0 : suggestionItems.Count - 1;
		else
			selectedSuggestionIndex = (selectedSuggestionIndex + direction + suggestionItems.Count) % suggestionItems.Count;

		UpdateSelectionClasses();
		ApplySuggestion( suggestionItems[selectedSuggestionIndex].Value, false );
	}

	void ApplySuggestion( string value )
	{
		ApplySuggestion( value, true );
	}

	void ApplySuggestion( string value, bool refreshSuggestions )
	{
		Text = value;
		CaretPosition = TextLength;

		if ( refreshSuggestions )
		{
			OnValueChanged();
		}
		else
		{
			preserveSuggestionListOnce = true;
			UpdateTypedMatchClasses();
		}

		Focus();
	}

	void UpdateSelectionClasses()
	{
		if ( !suggestionPanel.IsValid() )
			return;

		for ( var i = 0; i < suggestionPanel.Children.Count(); i++ )
		{
			suggestionPanel.Children.ElementAt( i ).SetClass( "active", i == selectedSuggestionIndex );
		}
	}

	void UpdateTypedMatchClasses()
	{
		if ( !suggestionPanel.IsValid() )
			return;

		var typedCommand = ExtractFirstToken( Text );
		for ( var i = 0; i < suggestionPanel.Children.Count(); i++ )
		{
			var item = i < suggestionItems.Count ? suggestionItems[i] : default;
			var isTypedMatch = !string.IsNullOrWhiteSpace( typedCommand )
				&& string.Equals( typedCommand, item.Command, StringComparison.OrdinalIgnoreCase );

			suggestionPanel.Children.ElementAt( i ).SetClass( "typed-match", isTypedMatch );
		}
	}

	static string ExtractFirstToken( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
			return string.Empty;

		var trimmed = value.TrimStart();
		var separator = trimmed.IndexOf( ' ' );
		return separator < 0 ? trimmed : trimmed[..separator];
	}
}
