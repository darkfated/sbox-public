namespace Sandbox.UI.Dev;

public class ConsoleTextEntry : TextEntry
{
	const int MaxVisibleSuggestions = 32;

	struct SuggestionItem
	{
		public string Title;
		public string Value;
		public string Command;
	}

	struct SuggestionRow
	{
		public Panel Row;
		public Label Name;
		public Label Value;
	}

	public Func<string, object[]> SuggestionProvider { get; set; }

	Panel suggestionPanel;
	Panel smartArgPanel;
	Panel smartArgControls;
	Label smartArgCommandLabel;

	readonly List<SuggestionItem> suggestionItems = new();
	readonly List<SuggestionRow> suggestionRows = new();
	int selectedSuggestionIndex = -1;
	bool preserveSuggestionListOnce;
	bool HasSuggestionItems => suggestionPanel.IsValid() && suggestionItems.Count > 0;

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
		switch ( e.Button )
		{
			case "tab":
			{
				var parsed = ParseInput( Text );
				if ( (parsed.ArgumentText != null && BuildCommandPanelModel( parsed ).IsValid) || e.HasShift )
				{
					e.StopPropagation = true;
					return;
				}

				if ( !suggestionPanel.IsValid() )
					UpdateSuggestionPopup();

				if ( HasSuggestionItems )
				{
					e.StopPropagation = true;
					MoveSuggestionSelection( 1 );
					return;
				}

				break;
			}
			case "up":
			case "down":
				e.StopPropagation = true;
				if ( HasSuggestionItems )
					MoveSuggestionSelection( e.Button == "up" ? -1 : 1 );
				return;
			case "escape":
				if ( suggestionPanel.IsValid() )
				{
					e.StopPropagation = true;
					DestroySuggestionPopup();
					return;
				}
				break;
		}

		base.OnButtonTyped( e );
	}

	public override void OnValueChanged()
	{
		base.OnValueChanged();
		UpdateInteractivePanels();
	}

	protected override void OnFocus( PanelEvent e )
	{
		base.OnFocus( e );
		UpdateInteractivePanels();
	}

	protected override void OnBlur( PanelEvent e )
	{
		base.OnBlur( e );
		DestroySuggestionPopup();
		DestroyCommandPanel();
	}

	public void FocusInput() => Focus();

	public void ClearInput()
	{
		Text = string.Empty;
		CaretPosition = 0;
		DestroySuggestionPopup();
		DestroyCommandPanel();
		OnValueChanged();
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
			if ( preserveSuggestionListOnce && HasSuggestionItems )
			{
				preserveSuggestionListOnce = false;
				UpdateSuggestionClasses( updateSelection: true, updateTypedMatch: true );
				return;
			}

			DestroySuggestionPopup();
			return;
		}

		preserveSuggestionListOnce = false;

		if ( !suggestionPanel.IsValid() || suggestionPanel.IsDeleting )
		{
			suggestionPanel = Add.Panel( "console-suggestion-menu" );
			suggestionPanel.SetClass( "hidden", true );
			suggestionRows.Clear();
		}
		suggestionPanel.SetClass( "hidden", false );
		suggestionPanel.ScrollOffset = 0;

		suggestionItems.Clear();
		selectedSuggestionIndex = -1;
		var visibleRowCount = 0;

		foreach ( var option in options )
		{
			if ( suggestionItems.Count >= MaxVisibleSuggestions )
				break;

			var item = ConvertOption( option );
			if ( string.IsNullOrWhiteSpace( item.Value ) )
				continue;

			item.Command = ExtractFirstToken( item.Value );
			suggestionItems.Add( item );
			var rawCurrentValue = string.IsNullOrWhiteSpace( item.Command )
				? null
				: ConsoleSystem.GetValue( item.Command );

			var row = EnsureSuggestionRow( visibleRowCount );
			row.Name.Text = item.Title;
			row.Value.Text = FormatSuggestionValue( rawCurrentValue );
			row.Row.UserData = item;
			row.Row.Style.Display = DisplayMode.Flex;

			if ( selectedSuggestionIndex < 0 && string.Equals( item.Value, Text, StringComparison.OrdinalIgnoreCase ) )
				selectedSuggestionIndex = visibleRowCount;

			visibleRowCount++;
		}

		HideSuggestionRows( visibleRowCount );
		TrimSuggestionRowPool();

		if ( suggestionItems.Count == 0 )
		{
			DestroySuggestionPopup();
			return;
		}

		UpdateSuggestionClasses( updateSelection: true, updateTypedMatch: true );
	}

	void DestroySuggestionPopup()
	{
		preserveSuggestionListOnce = false;
		suggestionPanel?.SetClass( "hidden", true );
		HideSuggestionRows();
		suggestionItems.Clear();
		selectedSuggestionIndex = -1;
		TrimSuggestionRowPool();
	}

	void HideSuggestionRows( int startIndex = 0 )
	{
		for ( var i = startIndex; i < suggestionRows.Count; i++ )
		{
			var row = suggestionRows[i].Row;
			if ( row.IsValid() )
				HideSuggestionRow( row );
		}
	}

	SuggestionRow EnsureSuggestionRow( int index )
	{
		while ( suggestionRows.Count <= index )
		{
			var rowPanel = suggestionPanel.Add.Panel( "suggestion-item" );
			var nameLabel = rowPanel.AddChild( new Label( string.Empty, "name" ) );
			var valueLabel = rowPanel.AddChild( new Label( string.Empty, "value" ) );
			rowPanel.AddEventListener( "onclick", () =>
			{
				if ( rowPanel.UserData is SuggestionItem item && !string.IsNullOrWhiteSpace( item.Value ) )
					ApplySuggestion( item.Value, true );
			} );

			suggestionRows.Add( new SuggestionRow
			{
				Row = rowPanel,
				Name = nameLabel,
				Value = valueLabel
			} );
		}

		return suggestionRows[index];
	}

	void TrimSuggestionRowPool()
	{
		if ( suggestionRows.Count <= MaxVisibleSuggestions )
			return;

		for ( var i = MaxVisibleSuggestions; i < suggestionRows.Count; i++ )
		{
			var row = suggestionRows[i].Row;
			if ( row.IsValid() )
				row.Delete( true );
		}

		suggestionRows.RemoveRange( MaxVisibleSuggestions, suggestionRows.Count - MaxVisibleSuggestions );
	}

	static void HideSuggestionRow( Panel row )
	{
		row.Style.Display = DisplayMode.None;
		row.UserData = null;
		row.SetClass( "active", false );
		row.SetClass( "typed-match", false );
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

		UpdateSuggestionClasses( updateSelection: true, updateTypedMatch: false );
		ApplySuggestion( suggestionItems[selectedSuggestionIndex].Value, false );
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
			UpdateSuggestionClasses( updateSelection: false, updateTypedMatch: true );
		}

		Focus();
	}

	void UpdateInteractivePanels()
	{
		var parsed = ParseInput( Text );
		var model = parsed.ArgumentText != null
			? BuildCommandPanelModel( parsed )
			: default;
		if ( model.IsValid )
		{
			DestroySuggestionPopup();
			UpdateCommandPanel( model );
			return;
		}

		DestroyCommandPanel();
		UpdateSuggestionPopup();
	}

	void UpdateCommandPanel( SmartArgumentModel model )
	{
		EnsureCommandPanel();
		smartArgPanel.SetClass( "hidden", false );
		smartArgCommandLabel.Text = model.Command;
		smartArgPanel.SetClass( "is-bool", model.IsBoolean );
		smartArgPanel.SetClass( "has-actions", model.IsBoolean || model.IsNumeric );
		smartArgControls.DeleteChildren( true );

		if ( model.IsBoolean )
		{
			BuildBooleanControls( model );
			return;
		}

		if ( model.IsNumeric )
			BuildNumericControls( model );
	}

	void EnsureCommandPanel()
	{
		if ( smartArgPanel.IsValid() && !smartArgPanel.IsDeleting )
			return;

		smartArgPanel = Add.Panel( "command-panel" );
		smartArgPanel.SetClass( "hidden", true );
		smartArgPanel.AddEventListener( "onmousedown", ( PanelEvent _ ) => Focus() );
		var header = smartArgPanel.Add.Panel( "header" );
		smartArgCommandLabel = header.AddChild( new Label( string.Empty, "command" ) );

		smartArgControls = smartArgPanel.Add.Panel( "controls" );
	}

	void DestroyCommandPanel()
	{
		if ( !smartArgPanel.IsValid() )
			return;

		smartArgPanel.SetClass( "hidden", true );
	}

	void BuildBooleanControls( SmartArgumentModel model )
	{
		var offButton = AddSmartArgButton( "OFF", () => SetInputArgumentValue( model.Command, "0" ) );
		var onButton = AddSmartArgButton( "ON", () => SetInputArgumentValue( model.Command, "1" ) );

		if ( !model.HasTypedBool )
			return;

		offButton.SetClass( "active", !model.TypedBool );
		onButton.SetClass( "active", model.TypedBool );
	}

	void BuildNumericControls( SmartArgumentModel model )
	{
		var effectiveModel = ResolveLatestNumericModel( model );
		var decimals = effectiveModel.DecimalPlaces;
		var smallStep = decimals > 0 ? 0.1 : 1.0;
		var largeStep = smallStep * 10.0;
		var smallStepLabel = decimals > 0 ? "0.1" : "1";
		var largeStepLabel = decimals > 0 ? "1" : "10";

		AddSmartArgButton( $"-{largeStepLabel}", () => ShiftNumericArgument( effectiveModel, -largeStep ) );
		AddSmartArgButton( $"-{smallStepLabel}", () => ShiftNumericArgument( effectiveModel, -smallStep ) );
		var currentButton = AddSmartArgButton( "current", () =>
		{
			var currentValue = ConsoleSystem.GetValue( effectiveModel.Command, effectiveModel.CurrentValue ) ?? effectiveModel.CurrentValue;
			SetInputArgumentValue( effectiveModel.Command, currentValue );
		} );
		currentButton.AddClass( "secondary" );
		AddSmartArgButton( $"+{smallStepLabel}", () => ShiftNumericArgument( effectiveModel, smallStep ) );
		AddSmartArgButton( $"+{largeStepLabel}", () => ShiftNumericArgument( effectiveModel, largeStep ) );
	}

	void ShiftNumericArgument( SmartArgumentModel model, double delta )
	{
		var effectiveModel = ResolveLatestNumericModel( model );
		var baseValue = effectiveModel.HasTypedNumber ? effectiveModel.TypedNumber : effectiveModel.CurrentNumber;
		var result = baseValue + delta;
		var decimals = Math.Clamp( effectiveModel.DecimalPlaces, 0, 4 );
		var rounded = Math.Round( result, decimals );
		var formatted = decimals == 0
			? rounded.ToString( System.Globalization.CultureInfo.InvariantCulture )
			: rounded.ToString( $"0.{new string( '#', decimals )}", System.Globalization.CultureInfo.InvariantCulture );
		SetInputArgumentValue( effectiveModel.Command, formatted );
	}

	SmartArgumentModel ResolveLatestNumericModel( SmartArgumentModel fallback )
	{
		var parsed = ParseInput( Text );
		if ( parsed.ArgumentText == null || !string.Equals( parsed.Command, fallback.Command, StringComparison.OrdinalIgnoreCase ) )
			return fallback;

		var latest = BuildCommandPanelModel( parsed );
		return latest.IsValid && latest.IsNumeric ? latest : fallback;
	}

	Button AddSmartArgButton( string text, Action onClick )
	{
		var button = smartArgControls.AddChild( new Button( text, onClick ) );
		button.AddClass( "arg-btn" );
		return button;
	}

	void SetInputArgumentValue( string command, string rawValue )
	{
		if ( string.IsNullOrWhiteSpace( command ) )
			return;

		var commandToken = command.Trim();
		var valueToken = PrepareArgumentToken( rawValue );
		Text = string.IsNullOrWhiteSpace( valueToken ) ? commandToken : $"{commandToken} {valueToken}";
		CaretPosition = TextLength;
		Focus();
	}

	static string PrepareArgumentToken( string value )
	{
		var text = value?.Trim() ?? string.Empty;
		if ( string.IsNullOrWhiteSpace( text ) )
			return string.Empty;

		if ( text.Contains( ' ' ) && !(text.StartsWith( '"' ) && text.EndsWith( '"' )) )
		{
			return $"\"{text.Replace( "\"", "\\\"" )}\"";
		}

		return text;
	}

	SmartArgumentModel BuildCommandPanelModel( ParsedInput parsed )
	{
		var currentValue = ConsoleSystem.GetValue( parsed.Command, null );
		if ( string.IsNullOrWhiteSpace( currentValue ) )
			return default;
		var hasTypedArgument = !string.IsNullOrWhiteSpace( parsed.ArgumentText );

		var model = new SmartArgumentModel
		{
			IsValid = true,
			Command = parsed.Command,
			CurrentValue = currentValue,
		};

		if ( TryParseBoolToken( currentValue, out _ ) )
		{
			model.IsBoolean = true;

			if ( hasTypedArgument && TryParseBoolToken( parsed.ArgumentText, out var typedBool ) )
			{
				model.HasTypedBool = true;
				model.TypedBool = typedBool;
			}

			return model;
		}

		if ( TryParseNumberToken( currentValue, out var currentNumber ) )
		{
			model.IsNumeric = true;
			model.CurrentNumber = currentNumber;
			model.DecimalPlaces = CountDecimalPlaces( currentValue );

			if ( hasTypedArgument && TryParseNumberToken( parsed.ArgumentText, out var typedNumber ) )
			{
				model.HasTypedNumber = true;
				model.TypedNumber = typedNumber;
				model.DecimalPlaces = Math.Max( model.DecimalPlaces, CountDecimalPlaces( parsed.ArgumentText ) );
			}

			return model;
		}

		return model;
	}

	static bool TryParseNumberToken( string value, out double number )
	{
		return double.TryParse(
			value,
			System.Globalization.NumberStyles.Float,
			System.Globalization.CultureInfo.InvariantCulture,
			out number );
	}

	static bool TryParseBoolToken( string value, out bool result )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
		{
			result = false;
			return false;
		}

		var raw = value.Trim();
		if ( raw == "1" )
		{
			result = true;
			return true;
		}
		if ( raw == "0" )
		{
			result = false;
			return true;
		}
		result = false;
		return false;
	}

	static string FormatSuggestionValue( string rawValue )
	{
		if ( TryParseBoolToken( rawValue, out var boolValue ) )
			return boolValue ? "True" : "False";

		return rawValue ?? string.Empty;
	}

	static int CountDecimalPlaces( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
			return 0;

		var raw = value.Trim();
		var dot = raw.IndexOf( '.' );
		if ( dot < 0 )
			return 0;

		var count = 0;
		for ( var i = dot + 1; i < raw.Length; i++ )
		{
			if ( !char.IsDigit( raw[i] ) )
				break;
			count++;
		}

		return count;
	}

	static ParsedInput ParseInput( string text )
	{
		var trimmed = text?.TrimStart();
		if ( string.IsNullOrWhiteSpace( trimmed ) )
			return default;

		var separator = trimmed.IndexOf( ' ' );
		if ( separator < 0 )
		{
			return new ParsedInput
			{
				Command = trimmed
			};
		}

		var argument = trimmed[(separator + 1)..];

		return new ParsedInput
		{
			Command = trimmed[..separator],
			ArgumentText = argument.Trim()
		};
	}

	struct ParsedInput
	{
		public string Command;
		public string ArgumentText;
	}

	struct SmartArgumentModel
	{
		public bool IsValid;
		public string Command;
		public string CurrentValue;

		public bool IsBoolean;
		public bool HasTypedBool;
		public bool TypedBool;

		public bool IsNumeric;
		public double CurrentNumber;
		public bool HasTypedNumber;
		public double TypedNumber;
		public int DecimalPlaces;
	}

	void UpdateSuggestionClasses( bool updateSelection, bool updateTypedMatch )
	{
		if ( !suggestionPanel.IsValid() )
			return;

		var typedCommand = updateTypedMatch ? ExtractFirstToken( Text ) : string.Empty;
		var hasTypedCommand = !string.IsNullOrWhiteSpace( typedCommand );
		var count = Math.Min( suggestionRows.Count, suggestionItems.Count );
		for ( var i = 0; i < count; i++ )
		{
			var row = suggestionRows[i].Row;
			if ( !row.IsValid() )
				continue;

			if ( updateSelection )
			{
				row.SetClass( "active", i == selectedSuggestionIndex );
			}

			if ( !updateTypedMatch )
				continue;

			var item = suggestionItems[i];
			var isTypedMatch = hasTypedCommand
				&& string.Equals( typedCommand, item.Command, StringComparison.OrdinalIgnoreCase );
			row.SetClass( "typed-match", isTypedMatch );
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
