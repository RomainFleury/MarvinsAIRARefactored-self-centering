using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

using MarvinsAIRARefactored.DataContext;

using AppSettings = MarvinsAIRARefactored.DataContext.DataContext;
using MarvinsAIRARefactored.Windows;

namespace MarvinsAIRARefactored.Pages;

public partial class ExperimentalWheelPage : System.Windows.Controls.UserControl
{
	private readonly DispatcherTimer _liveUiTimer = new() { Interval = TimeSpan.FromMilliseconds( 50 ) };

	private float _axisMapLastNormalized;

	private bool _axisMapLastHavePosition;

	private readonly float[] _previewCenteringHistory = new float[ 180 ];

	private int _previewCenteringHead;

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

	private void UpdateLiveSteeringUi()
	{
		var localization = AppSettings.Instance.Localization;
		var settings = AppSettings.Instance.Settings;
		var g = settings.RacingWheelSteeringDeviceGuid;

		if ( g == Guid.Empty )
		{
			SteeringAxisLive_TextBlock.Text = localization[ "ExperimentalWheelAxisNoDevice" ];
			SteeringAxisZone_TextBlock.Text = string.Empty;
			_axisMapLastHavePosition = false;
			UpdateAxisMapVisual( -1f, false );

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
			SteeringAxisZone_TextBlock.Text = BuildZoneStatusText( localization, settings, normalized );
			_axisMapLastNormalized = normalized;
			_axisMapLastHavePosition = true;
			UpdateAxisMapVisual( normalized, true );
		}
		else
		{
			SteeringAxisLive_TextBlock.Text = localization[ "ExperimentalWheelAxisNoData" ];
			SteeringAxisZone_TextBlock.Text = string.Empty;
			_axisMapLastHavePosition = false;
			UpdateAxisMapVisual( -1f, false );
		}
	}

	private static string BuildZoneStatusText( global::MarvinsAIRARefactored.Components.Localization localization, global::MarvinsAIRARefactored.DataContext.Settings settings, float normalized )
	{
		var abs = MathF.Abs( normalized );
		var bumpOn = settings.ExperimentalWheelCenterBumpEnabled && settings.ExperimentalWheelCenterBumpStrength > 0f;
		var inner = settings.ExperimentalWheelCenterBumpInner;
		var outer = settings.ExperimentalWheelCenterBumpOuter;
		var softOn = settings.ExperimentalWheelSoftLockEnabled && settings.ExperimentalWheelSoftLockStrength > 0f;
		var th = settings.ExperimentalWheelSoftLockThreshold;

		if ( bumpOn )
		{
			if ( abs < inner )
			{
				return localization[ "ExperimentalWheelZoneDeadZone" ];
			}

			if ( abs <= outer )
			{
				return localization[ "ExperimentalWheelZoneOnBump" ];
			}

			if ( softOn && ( abs > th ) )
			{
				return localization[ "ExperimentalWheelZoneSoftLock" ];
			}

			if ( softOn && ( abs <= th ) )
			{
				return localization[ "ExperimentalWheelZoneMidTravel" ];
			}

			return localization[ "ExperimentalWheelZoneAfterBump" ];
		}

		const float centeredDeadband = 0.02f;

		if ( abs <= centeredDeadband )
		{
			return localization[ "ExperimentalWheelZoneCentered" ];
		}

		if ( softOn && ( abs > th ) )
		{
			return localization[ "ExperimentalWheelZoneSoftLock" ];
		}

		return localization[ "ExperimentalWheelZoneMidTravel" ];
	}

	private void AxisMapHost_SizeChanged( object sender, SizeChangedEventArgs e )
	{
		if ( sender is FrameworkElement fe && fe.ActualWidth > 0d )
		{
			AxisMapCanvas.Width = fe.ActualWidth;
		}

		UpdateAxisMapVisual( _axisMapLastNormalized, _axisMapLastHavePosition );
	}

	private void UpdateAxisMapVisual( float normalized, bool havePosition )
	{
		var w = AxisMapHost.ActualWidth;

		if ( w <= 1d )
		{
			AxisMapNeedle.Visibility = Visibility.Collapsed;

			return;
		}

		AxisMapCanvas.Width = w;
		var wf = (float) w;
		var settings = AppSettings.Instance.Settings;

		static float PosToCanvasX( float pos, float widthf ) => ( pos + 1f ) * 0.5f * widthf;

		var bumpOn = settings.ExperimentalWheelCenterBumpEnabled && settings.ExperimentalWheelCenterBumpStrength > 0f;
		var inner = settings.ExperimentalWheelCenterBumpInner;
		var outer = settings.ExperimentalWheelCenterBumpOuter;
		var softOn = settings.ExperimentalWheelSoftLockEnabled && settings.ExperimentalWheelSoftLockStrength > 0f;
		var th = settings.ExperimentalWheelSoftLockThreshold;

		if ( bumpOn && ( outer > inner ) && ( inner > 0f ) )
		{
			var xNegOuter = PosToCanvasX( -outer, wf );
			var xNegInner = PosToCanvasX( -inner, wf );
			var xPosInner = PosToCanvasX( inner, wf );
			var xPosOuter = PosToCanvasX( outer, wf );

			AxisMapBumpFillNeg.Visibility = Visibility.Visible;
			Canvas.SetLeft( AxisMapBumpFillNeg, xNegOuter );
			AxisMapBumpFillNeg.Width = Math.Max( 0d, xNegInner - xNegOuter );

			AxisMapBumpFillPos.Visibility = Visibility.Visible;
			Canvas.SetLeft( AxisMapBumpFillPos, xPosInner );
			AxisMapBumpFillPos.Width = Math.Max( 0d, xPosOuter - xPosInner );

			AxisMapBumpLineNegOuter.Visibility = Visibility.Visible;
			AxisMapBumpLineNegInner.Visibility = Visibility.Visible;
			AxisMapBumpLinePosInner.Visibility = Visibility.Visible;
			AxisMapBumpLinePosOuter.Visibility = Visibility.Visible;
			Canvas.SetLeft( AxisMapBumpLineNegOuter, xNegOuter - 1d );
			Canvas.SetLeft( AxisMapBumpLineNegInner, xNegInner - 0.5d );
			Canvas.SetLeft( AxisMapBumpLinePosInner, xPosInner - 0.5d );
			Canvas.SetLeft( AxisMapBumpLinePosOuter, xPosOuter - 1d );
		}
		else
		{
			AxisMapBumpFillNeg.Visibility = Visibility.Collapsed;
			AxisMapBumpFillPos.Visibility = Visibility.Collapsed;
			AxisMapBumpLineNegOuter.Visibility = Visibility.Collapsed;
			AxisMapBumpLineNegInner.Visibility = Visibility.Collapsed;
			AxisMapBumpLinePosInner.Visibility = Visibility.Collapsed;
			AxisMapBumpLinePosOuter.Visibility = Visibility.Collapsed;
		}

		var softLockOpacity = softOn ? 1d : 0.35d;

		AxisMapSoftLockLeft.Opacity = softLockOpacity;
		AxisMapSoftLockRight.Opacity = softLockOpacity;
		Canvas.SetLeft( AxisMapSoftLockLeft, PosToCanvasX( -th, wf ) - 1d );
		Canvas.SetLeft( AxisMapSoftLockRight, PosToCanvasX( th, wf ) - 1d );

		Canvas.SetLeft( AxisMapCenterLine, PosToCanvasX( 0f, wf ) - 1d );

		if ( havePosition && ( normalized >= -1f ) && ( normalized <= 1f ) )
		{
			AxisMapNeedle.Visibility = Visibility.Visible;
			Canvas.SetLeft( AxisMapNeedle, PosToCanvasX( normalized, wf ) - AxisMapNeedle.Width * 0.5d );
			Canvas.SetTop( AxisMapNeedle, ( AxisMapCanvas.Height - AxisMapNeedle.Height ) * 0.5d );
		}
		else
		{
			AxisMapNeedle.Visibility = Visibility.Collapsed;
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

		UpdateLiveSteeringUi();
		UpdatePreviewCenteringForceDisplay( app );
	}

	private void UpdatePreviewCenteringForceDisplay( MarvinsAIRARefactored.App app )
	{
		var localization = AppSettings.Instance.Localization;

		if ( app.StandaloneCenteringSessionActive && !app.Simulator.IsConnected )
		{
			var t = app.RacingWheel.ExperimentalSessionLastCenteringTorque;
			PreviewCenteringValue_TextBlock.Text = string.Format( localization[ "ExperimentalWheelPreviewCenteringFormat" ], t );
		}
		else
		{
			PreviewCenteringValue_TextBlock.Text = localization[ "ExperimentalWheelPreviewCenteringSessionOff" ];
		}

		PushPreviewCenteringSample( app.StandaloneCenteringSessionActive && !app.Simulator.IsConnected ? app.RacingWheel.ExperimentalSessionLastCenteringTorque : 0f );
		RedrawPreviewCenteringPolyline();
	}

	private void PushPreviewCenteringSample( float value )
	{
		_previewCenteringHistory[ _previewCenteringHead ] = value;
		_previewCenteringHead = ( _previewCenteringHead + 1 ) % _previewCenteringHistory.Length;
	}

	private void RedrawPreviewCenteringPolyline()
	{
		var w = PreviewForceCanvas.ActualWidth;
		var h = PreviewForceCanvas.ActualHeight;

		if ( ( w <= 1d ) || ( h <= 1d ) )
		{
			return;
		}

		var midY = (float) ( h * 0.5 );
		var scaleY = (float) ( h * 0.42 );
		var pts = new PointCollection();
		var n = _previewCenteringHistory.Length;

		for ( var i = 0; i < n; i++ )
		{
			var idx = ( _previewCenteringHead + i ) % n;
			var v = _previewCenteringHistory[ idx ];
			var x = w * i / ( n - 1 );
			var y = (double) ( midY - v * scaleY );

			pts.Add( new System.Windows.Point( x, y ) );
		}

		PreviewForcePolyline.Points = pts;
	}

	private void PreviewForceArea_SizeChanged( object sender, SizeChangedEventArgs e )
	{
		if ( sender is not FrameworkElement fe )
		{
			return;
		}

		var w = fe.ActualWidth;
		var h = fe.ActualHeight;

		PreviewForceCanvas.Width = w;
		PreviewForceCanvas.Height = h;
		PreviewForceMidLine.X1 = 0d;
		PreviewForceMidLine.X2 = w;
		PreviewForceMidLine.Y1 = h * 0.5d;
		PreviewForceMidLine.Y2 = h * 0.5d;

		RedrawPreviewCenteringPolyline();
	}

	private void ExperimentalSteeringDevice_MairaComboBox_SelectionChanged( object sender, SelectionChangedEventArgs e )
	{
		UpdateSelectedSteeringDeviceDisplay();
		UpdateLiveSteeringUi();
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
		UpdateLiveSteeringUi();

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
