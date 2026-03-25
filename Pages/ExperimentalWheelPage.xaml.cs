using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using AppSettings = MarvinsAIRARefactored.DataContext.DataContext;
using MarvinsAIRARefactored.Windows;

namespace MarvinsAIRARefactored.Pages;

public partial class ExperimentalWheelPage : System.Windows.Controls.UserControl
{
	private readonly DispatcherTimer _liveUiTimer = new() { Interval = TimeSpan.FromMilliseconds( 50 ) };

	public ExperimentalWheelPage()
	{
		InitializeComponent();

		_liveUiTimer.Tick += LiveUiTimer_Tick;

		Loaded += Root_Loaded;
	}

	private void RefreshSteeringDeviceCombo()
	{
		var app = MarvinsAIRARefactored.App.Instance;

		if ( app == null )
		{
			return;
		}

		var localization = AppSettings.Instance.Localization;
		var settings = AppSettings.Instance.Settings;

		var dictionary = new Dictionary<Guid, string>();

		if ( app.DirectInput.ForceFeedbackDeviceList.Count == 0 )
		{
			dictionary.Add( Guid.Empty, localization[ "NoFFBDevicesFound" ] );
		}
		else
		{
			dictionary.Add( Guid.Empty, localization[ "FFBDeviceNotSelected" ] );
		}

		foreach ( var keyValuePair in app.DirectInput.ForceFeedbackDeviceList.ToList() )
		{
			dictionary[ keyValuePair.Key ] = keyValuePair.Value;
		}

		if ( !dictionary.ContainsKey( settings.RacingWheelSteeringDeviceGuid ) )
		{
			dictionary.Add( settings.RacingWheelSteeringDeviceGuid, $"{localization[ "DeviceNotFound" ]} [{settings.RacingWheelSteeringDeviceGuid}]" );
		}

		ExperimentalSteeringDevice_MairaComboBox.ItemsSource = dictionary.OrderBy( keyValuePair => keyValuePair.Value ).ToList();
		ExperimentalSteeringDevice_MairaComboBox.SelectedValue = settings.RacingWheelSteeringDeviceGuid;
		ExperimentalSteeringDevice_MairaComboBox.OffValue = Guid.Empty;
	}

	private void UpdateSelectedSteeringDeviceDisplay()
	{
		var localization = AppSettings.Instance.Localization;
		var settings = AppSettings.Instance.Settings;
		var g = settings.RacingWheelSteeringDeviceGuid;

		if ( g == Guid.Empty )
		{
			SelectedSteeringDevice_TextBlock.Text = localization[ "ExperimentalWheelNoDeviceSelected" ];

			return;
		}

		var app = MarvinsAIRARefactored.App.Instance;

		if ( ( app != null ) && app.DirectInput.ForceFeedbackDeviceList.TryGetValue( g, out var name ) )
		{
			SelectedSteeringDevice_TextBlock.Text = string.Format( localization[ "ExperimentalWheelSelectedDeviceFormat" ], name );
		}
		else
		{
			SelectedSteeringDevice_TextBlock.Text = string.Format( localization[ "ExperimentalWheelSelectedDeviceMissingFormat" ], g );
		}
	}

	private void UpdateSteeringAxisDisplay()
	{
		var localization = AppSettings.Instance.Localization;
		var settings = AppSettings.Instance.Settings;
		var g = settings.RacingWheelSteeringDeviceGuid;

		if ( g == Guid.Empty )
		{
			SteeringAxisLive_TextBlock.Text = localization[ "ExperimentalWheelAxisNoDevice" ];

			return;
		}

		var app = MarvinsAIRARefactored.App.Instance;

		if ( app == null )
		{
			return;
		}

		if ( app.DirectInput.TryGetNormalizedSteeringAxis( g, out var normalized ) )
		{
			SteeringAxisLive_TextBlock.Text = string.Format( localization[ "ExperimentalWheelAxisFormat" ], normalized );
		}
		else
		{
			SteeringAxisLive_TextBlock.Text = localization[ "ExperimentalWheelAxisNoData" ];
		}
	}

	private void LiveUiTimer_Tick( object? sender, EventArgs e )
	{
		if ( !IsLoaded )
		{
			return;
		}

		var app = MarvinsAIRARefactored.App.Instance;

		if ( app == null )
		{
			return;
		}

		app.DirectInput.PollDevices( 0.05f );

		UpdateSteeringAxisDisplay();
	}

	private void ExperimentalSteeringDevice_MairaComboBox_SelectionChanged( object sender, SelectionChangedEventArgs e )
	{
		UpdateSelectedSteeringDeviceDisplay();
		UpdateSteeringAxisDisplay();
	}

	private void Root_Loaded( object sender, RoutedEventArgs e )
	{
		var app = MarvinsAIRARefactored.App.Instance;

		if ( app != null )
		{
			SessionActive_MairaSwitch.IsOn = app.StandaloneCenteringSessionActive;
		}

		RefreshSteeringDeviceCombo();
		UpdateSelectedSteeringDeviceDisplay();
		UpdateSteeringAxisDisplay();

		_liveUiTimer.Start();
	}

	private void SessionActive_MairaSwitch_Toggled( object sender, System.EventArgs e )
	{
		var app = MarvinsAIRARefactored.App.Instance;

		if ( app == null || !app.Ready )
		{
			return;
		}

		var settings = AppSettings.Instance.Settings;

		if ( SessionActive_MairaSwitch.IsOn )
		{
			if ( settings.RacingWheelSteeringDeviceGuid == Guid.Empty )
			{
				SessionActive_MairaSwitch.IsOn = false;

				System.Windows.MessageBox.Show(
					AppSettings.Instance.Localization[ "ExperimentalWheelSelectDeviceBeforeSession" ],
					AppSettings.Instance.Localization[ "ExperimentalWheel_UC" ],
					MessageBoxButton.OK,
					MessageBoxImage.Information );

				return;
			}

			if ( !settings.RacingWheelEnableForceFeedback )
			{
				SessionActive_MairaSwitch.IsOn = false;

				System.Windows.MessageBox.Show(
					AppSettings.Instance.Localization[ "ExperimentalWheelEnableFFBFirst" ],
					AppSettings.Instance.Localization[ "ExperimentalWheel_UC" ],
					MessageBoxButton.OK,
					MessageBoxImage.Information );

				return;
			}

			if ( app.Simulator.IsConnected )
			{
				SessionActive_MairaSwitch.IsOn = false;

				System.Windows.MessageBox.Show(
					AppSettings.Instance.Localization[ "ExperimentalWheelIRacingConnected" ],
					AppSettings.Instance.Localization[ "ExperimentalWheel_UC" ],
					MessageBoxButton.OK,
					MessageBoxImage.Information );

				return;
			}

			app.StandaloneCenteringSessionActive = true;
		}
		else
		{
			app.DeactivateStandaloneCenteringSession();
		}

		MainWindow._racingWheelPage.UpdateSteeringDeviceSection();
	}

	private void Root_Unloaded( object sender, RoutedEventArgs e )
	{
		_liveUiTimer.Stop();

		MarvinsAIRARefactored.App.Instance?.DeactivateStandaloneCenteringSession();
	}
}
