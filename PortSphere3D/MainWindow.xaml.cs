using System.Net;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Media3D;

namespace PortSphere3D
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // 3D scene fields
        private Model3DGroup _sceneGroup = null!;
        private GeometryModel3D _sphereModel = null!;
        private DiffuseMaterial _sphereMaterial = null!;
        private DrawingBrush _gridBrush = null!;
        private DrawingBrush _octagonBrush = null!;
    private DrawingBrush _starsBrush = null!;
    private DrawingBrush _baseTextureBrush = null!;
        private PointLight _fixedLight = null!;
        private PointLight _draggableLight1 = null!;
        private PointLight _draggableLight2 = null!;
        private PointLight _draggableLight3 = null!;
        private PointLight _draggableLight4 = null!;
        private PointLight _viewerLight = null!;
    private Ellipse _handle1 = null!;
    private Ellipse _handle2 = null!;
    private Ellipse _handle3 = null!;
    private Ellipse _handle4 = null!;
    private TextBlock _zText1 = null!;
    private TextBlock _zText2 = null!;
    private TextBlock _zText3 = null!;
    private TextBlock _zText4 = null!;
    private TextBlock _zInstruction = null!;
        private AxisAngleRotation3D _rotation = null!;
    private ScaleTransform3D _scale = null!;
    private Transform3DGroup _sphereTransforms = null!;
        private double _sphereRadius = 2.1;
    private double _spinSpeedBase = 0.3; // degrees per frame (magnitude)
    private Vector2D _spinDirVec = new Vector2D(1, 0); // unit vector in XY plane; X=horizontal, Y=vertical
    private bool _dialDragging = false;
    private TextBlock _label1 = null!;
    private TextBlock _label2 = null!;
    private TextBlock _label3 = null!;
    private TextBlock _label4 = null!;
        private bool _dragging1, _dragging2, _dragging3, _dragging4;
        private Point _lastMousePos;
        private Thread? _httpThread;
        private HttpListener? _httpListener;
    private string _currentTextureName = "Grid";

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
            this.LightHandlesCanvas!.SizeChanged += LightHandlesCanvas_SizeChanged;
            this.LightHandlesCanvas.MouseLeftButtonDown += LightHandlesCanvas_MouseLeftButtonDown;
            this.LightHandlesCanvas.MouseLeftButtonUp += LightHandlesCanvas_MouseLeftButtonUp;
            this.LightHandlesCanvas.MouseMove += LightHandlesCanvas_MouseMove;
            this.LightHandlesCanvas.MouseWheel += LightHandlesCanvas_MouseWheel;
            StartMcpServer();
        }
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try { _httpListener?.Stop(); } catch { }
            try { _httpListener?.Close(); } catch { }
            _httpListener = null;
            try { _httpThread?.Join(250); } catch { }
            _httpThread = null;
        }

        private void TextureComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_sphereMaterial == null) return;
            var item = (TextureComboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Grid";
            _currentTextureName = item;
            if (item.Contains("Octagon", StringComparison.OrdinalIgnoreCase))
                _baseTextureBrush = _octagonBrush;
            else if (item.Contains("Stars", StringComparison.OrdinalIgnoreCase))
                _baseTextureBrush = _starsBrush;
            else
                _baseTextureBrush = _gridBrush;
            ApplyTintAndSetMaterial();
        }

        private void SphereSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _sphereRadius = e.NewValue;
            if (_scale != null)
            {
                var s = _sphereRadius; // base mesh radius = 1
                _scale.ScaleX = s;
                _scale.ScaleY = s;
                _scale.ScaleZ = s;
            }
        }

        private void SetupBrushes()
        {
            // Grid brush
            var dg1 = new DrawingGroup();
            var bg = new GeometryDrawing(new SolidColorBrush(Color.FromRgb(30, 90, 30)), null, new RectangleGeometry(new Rect(0, 0, 100, 100)));
            dg1.Children.Add(bg);
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(0, 180, 0)), 1);
            for (int i = 0; i <= 10; i++)
            {
                double t = i * 10;
                dg1.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(t, 0), new Point(t, 100))));
                dg1.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(0, t), new Point(100, t))));
            }
            _gridBrush = new DrawingBrush(dg1) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 0.2, 0.2), ViewportUnits = BrushMappingMode.RelativeToBoundingBox, Viewbox = new Rect(0, 0, 100, 100), ViewboxUnits = BrushMappingMode.Absolute };

            // Octagon-ish brush (using rotated grid overlay to simulate)
            var dg2 = new DrawingGroup();
            dg2.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(30, 30, 90)), null, new RectangleGeometry(new Rect(0, 0, 100, 100))));
            var pen2 = new Pen(new SolidColorBrush(Color.FromRgb(0, 200, 200)), 1);
            for (int i = -10; i <= 10; i++)
            {
                double t = i * 10;
                dg2.Children.Add(new GeometryDrawing(null, pen2, new LineGeometry(new Point(t + 50, -50), new Point(t + 50, 150))));
                dg2.Children.Add(new GeometryDrawing(null, pen2, new LineGeometry(new Point(-50, t + 50), new Point(150, t + 50))));
            }
            _octagonBrush = new DrawingBrush(dg2) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 0.25, 0.25), ViewportUnits = BrushMappingMode.RelativeToBoundingBox, Viewbox = new Rect(0, 0, 100, 100), ViewboxUnits = BrushMappingMode.Absolute };

            // Stars brush: dark sky background with small star ellipses
            var dg3 = new DrawingGroup();
            dg3.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(5, 8, 16)), null, new RectangleGeometry(new Rect(0, 0, 100, 100))));
            // Deterministic star positions (x, y, radius, opacity)
            var stars = new (double x, double y, double r, double a)[]
            {
                (10, 12, 1.2, 0.9), (25, 8, 0.9, 0.8), (42, 16, 1.1, 0.85), (60, 10, 0.8, 0.8), (78, 14, 1.3, 0.95),
                (15, 32, 0.7, 0.7), (33, 28, 0.9, 0.8), (50, 34, 1.0, 0.9), (68, 30, 0.8, 0.75), (88, 26, 0.7, 0.7),
                (6, 54, 1.0, 0.9), (24, 48, 0.8, 0.8), (40, 52, 0.9, 0.85), (58, 46, 1.1, 0.9), (76, 50, 0.8, 0.75),
                (12, 76, 0.8, 0.8), (28, 70, 1.2, 0.9), (46, 72, 0.9, 0.85), (64, 78, 0.7, 0.75), (84, 74, 1.1, 0.92),
                (20, 92, 0.9, 0.85), (38, 88, 0.8, 0.8), (56, 94, 1.3, 0.95), (72, 86, 0.9, 0.85), (90, 90, 0.8, 0.8)
            };
            foreach (var s in stars)
            {
                var starFill = new SolidColorBrush(Color.FromArgb((byte)(s.a * 255), 255, 255, 255));
                dg3.Children.Add(new GeometryDrawing(starFill, null, new EllipseGeometry(new Point(s.x, s.y), s.r, s.r)));
            }
            _starsBrush = new DrawingBrush(dg3)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 0.3, 0.3),
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewbox = new Rect(0, 0, 100, 100),
                ViewboxUnits = BrushMappingMode.Absolute
            };

            // Default base brush
            _baseTextureBrush = _gridBrush;
        }

        private void Setup3DScene()
        {
            _sceneGroup = new Model3DGroup();

            // Camera
            var cam = new PerspectiveCamera
            {
                Position = new Point3D(0, 0, 10),
                LookDirection = new Vector3D(0, 0, -10),
                UpDirection = new Vector3D(0, 1, 0),
                FieldOfView = 45
            };
            this.MainViewport!.Camera = cam;

            // Sphere material
            _sphereMaterial = new DiffuseMaterial(_baseTextureBrush ?? _gridBrush);
            var spec = new SpecularMaterial(new SolidColorBrush(Color.FromRgb(255,255,255)), 80);
            var mg = new MaterialGroup();
            mg.Children.Add(_sphereMaterial);
            mg.Children.Add(spec);

            // Sphere mesh (base radius 1)
            var mesh = CreateSphereMesh(1.0, 64, 32);
            _sphereModel = new GeometryModel3D(mesh, mg);

            // Transforms: rotation + scale
            _rotation = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
            var r = new RotateTransform3D(_rotation);
            _scale = new ScaleTransform3D(_sphereRadius, _sphereRadius, _sphereRadius);
            _sphereTransforms = new Transform3DGroup();
            _sphereTransforms.Children.Add(r);
            _sphereTransforms.Children.Add(_scale);
            _sphereModel.Transform = _sphereTransforms;

            _sceneGroup.Children.Add(_sphereModel);

            // Ambient light for visibility
            _sceneGroup.Children.Add(new AmbientLight(Color.FromRgb(8, 8, 8)));

            var mv3d = new ModelVisual3D { Content = _sceneGroup };
            this.MainViewport.Children.Clear();
            this.MainViewport.Children.Add(mv3d);
        }

        private void SetupLights()
        {
            // Viewer light (static, from camera)
            _viewerLight = new PointLight(Colors.White, new Point3D(0, 0, 10)) { Range = 80, ConstantAttenuation = 0.4, LinearAttenuation = 0.06 };
            _sceneGroup.Children.Add(_viewerLight);

            // Fixed light
            _fixedLight = new PointLight(Color.FromRgb(255, 220, 180), new Point3D(6, 6, 2.5)) { Range = 80, ConstantAttenuation = 0.5, LinearAttenuation = 0.08 };
            _sceneGroup.Children.Add(_fixedLight);

            // Draggable lights
            _draggableLight1 = new PointLight(Colors.Red, new Point3D(-4.5, 0, -1.0)) { Range = 120, ConstantAttenuation = 0.9, LinearAttenuation = 0.03 };
            _draggableLight2 = new PointLight(Colors.Lime, new Point3D(4.5, 0, -1.0)) { Range = 120, ConstantAttenuation = 0.9, LinearAttenuation = 0.03 };
            _draggableLight3 = new PointLight(Colors.DeepSkyBlue, new Point3D(0, 4.5, -1.0)) { Range = 120, ConstantAttenuation = 0.9, LinearAttenuation = 0.03 };
            _draggableLight4 = new PointLight(Colors.Gold, new Point3D(0, -4.5, -1.0)) { Range = 120, ConstantAttenuation = 0.9, LinearAttenuation = 0.03 };
            _sceneGroup.Children.Add(_draggableLight1);
            _sceneGroup.Children.Add(_draggableLight2);
            _sceneGroup.Children.Add(_draggableLight3);
            _sceneGroup.Children.Add(_draggableLight4);
        }

        private void SetupHandles()
        {
            _handle1 = MakeHandle(Colors.White);
            _handle2 = MakeHandle(Colors.White);
            _handle3 = MakeHandle(Colors.LightYellow);
            _handle4 = MakeHandle(Colors.LightYellow);
            _zText1 = MakeZText();
            _zText2 = MakeZText();
            _zText3 = MakeZText();
            _zText4 = MakeZText();
            _label1 = MakeGreekLabel("α");
            _label2 = MakeGreekLabel("β");
            _label3 = MakeGreekLabel("γ");
            _label4 = MakeGreekLabel("δ");
        if (_zInstruction == null)
            {
                _zInstruction = new TextBlock
                {
            Text = "Tip: Use mouse wheel over a handle to adjust Z (depth). Right-click a handle to change its color.",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 16,
                    Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
                    Padding = new Thickness(8, 2, 8, 2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    IsHitTestVisible = false
                };
            }
            this.LightHandlesCanvas!.Children.Clear();
            this.LightHandlesCanvas.Children.Add(_zInstruction);
            Canvas.SetZIndex(_zInstruction, 1000);
            this.LightHandlesCanvas.Children.Add(_handle1);
            this.LightHandlesCanvas.Children.Add(_zText1);
            this.LightHandlesCanvas.Children.Add(_label1);
            AddLightControlForHandle(_handle1, 1);
            this.LightHandlesCanvas.Children.Add(_handle2);
            this.LightHandlesCanvas.Children.Add(_zText2);
            this.LightHandlesCanvas.Children.Add(_label2);
            AddLightControlForHandle(_handle2, 2);
            this.LightHandlesCanvas.Children.Add(_handle3);
            this.LightHandlesCanvas.Children.Add(_zText3);
            this.LightHandlesCanvas.Children.Add(_label3);
            AddLightControlForHandle(_handle3, 3);
            this.LightHandlesCanvas.Children.Add(_handle4);
            this.LightHandlesCanvas.Children.Add(_zText4);
            this.LightHandlesCanvas.Children.Add(_label4);
            AddLightControlForHandle(_handle4, 4);
            UpdateHandlePositions();
        }

        private sealed class LightHandleUi
        {
            public required Polygon Triangle { get; init; }
            public required ComboBox Combo { get; init; }
        }

        private void AddLightControlForHandle(Ellipse handle, int index)
        {
            // Create a small triangle indicating direction (points upwards by default)
            var triangle = new Polygon
            {
                Points = new PointCollection(new[] { new Point(0, 10), new Point(6, 22), new Point(-6, 22) }),
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 0.5,
                Tag = index,
                IsHitTestVisible = false
            };
            Canvas.SetZIndex(triangle, 2);
            this.LightHandlesCanvas!.Children.Add(triangle);

            // Create a small popup-ish panel for color selection near the handle
            var combo = new ComboBox
            {
                Width = 90,
                Visibility = Visibility.Collapsed,
                Tag = index
            };
            foreach (var name in new[] { "Red","Orange","Yellow","Lime","Cyan","DeepSkyBlue","Blue","Magenta","Gold","White" })
                combo.Items.Add(new ComboBoxItem { Content = name });
            combo.SelectionChanged += (s, e) =>
            {
                var cb = (ComboBox)s!;
                var sel = (cb.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "White";
                var color = ParseNamedColor(sel);
                switch (index)
                {
                    case 1: if (_draggableLight1 != null) _draggableLight1.Color = color; break;
                    case 2: if (_draggableLight2 != null) _draggableLight2.Color = color; break;
                    case 3: if (_draggableLight3 != null) _draggableLight3.Color = color; break;
                    case 4: if (_draggableLight4 != null) _draggableLight4.Color = color; break;
                }
            };
            this.LightHandlesCanvas.Children.Add(combo);

            // Show/hide on handle click
            handle.MouseRightButtonUp += (s, e) =>
            {
                combo.Visibility = combo.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                e.Handled = true;
            };
            handle.ToolTip = "Drag to move; mouse wheel adjusts Z; right-click for color";

            // Store refs via Tag for positioning in PositionHandle
            handle.Tag = new LightHandleUi { Triangle = triangle, Combo = combo };
        }
        private TextBlock MakeZText()
        {
            return new TextBlock
            {
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(2, 0, 2, 0),
                TextAlignment = TextAlignment.Center,
                Width = 22,
                Height = 16,
                Text = "0.0",
                IsHitTestVisible = false
            };
        }

        private TextBlock MakeGreekLabel(string letter)
        {
            return new TextBlock
            {
                Foreground = Brushes.Black,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                Padding = new Thickness(2, 0, 2, 0),
                TextAlignment = TextAlignment.Center,
                Width = 18,
                Height = 16,
                Text = letter,
                IsHitTestVisible = false
            };
        }

        private Ellipse MakeHandle(Color c)
        {
            return new Ellipse
            {
                Width = 16,
                Height = 16,
                Stroke = new SolidColorBrush(Colors.Black),
                Fill = new SolidColorBrush(c),
                StrokeThickness = 1,
                Opacity = 0.9
            };
        }

        private void StartAnimation()
        {
            CompositionTarget.Rendering += (_, __) =>
            {
                if (_rotation != null)
                {
                    // Use horizontal component of direction to control sign, vertical can be used later for tilt
                    var sign = Math.Sign(_spinDirVec.X);
                    double step = sign * _spinSpeedBase;
                    _rotation.Angle = (_rotation.Angle + step) % 360.0;
                }
            };
        }

        // Map Canvas point to a world point on z = 3 plane, assuming a viewport roughly mapping -5..5 in X/Y
        private Point3D UnprojectFrom2D(Point pt, Point3D ref3D)
        {
            double w = this.LightHandlesCanvas!.ActualWidth;
            double h = this.LightHandlesCanvas!.ActualHeight;
            if (w < 1 || h < 1) return ref3D;
            double worldX = (pt.X / w - 0.5) * 10.0;
            double worldY = (0.5 - pt.Y / h) * 10.0;
            double worldZ = ref3D.Z; // keep current z to avoid jumps
            return new Point3D(worldX, worldY, worldZ);
        }

        // Map world point to Canvas point using the same linear mapping
        private void PositionHandle(Ellipse handle, Point3D pos)
        {
            double w = this.LightHandlesCanvas!.ActualWidth;
            double h = this.LightHandlesCanvas!.ActualHeight;
            if (w < 1 || h < 1) return;
            double x = (pos.X / 10.0 + 0.5) * w;
            double y = (0.5 - (pos.Y / 10.0)) * h;
            Canvas.SetLeft(handle, x - handle.Width / 2);
            Canvas.SetTop(handle, y - handle.Height / 2);
            handle.ToolTip = $"Z: {pos.Z:F2}";

            // Find which zText to update
            TextBlock? zText = null;
            if (handle == _handle1) zText = _zText1;
            else if (handle == _handle2) zText = _zText2;
            else if (handle == _handle3) zText = _zText3;
            else if (handle == _handle4) zText = _zText4;
            if (zText != null)
            {
                zText.Text = pos.Z.ToString("F1");
                // Center the text over the handle
                Canvas.SetLeft(zText, x - zText.Width / 2);
                Canvas.SetTop(zText, y - zText.Height / 2);
            }

            // Position Greek letter slightly below the handle
            TextBlock? label = null;
            if (handle == _handle1) label = _label1;
            else if (handle == _handle2) label = _label2;
            else if (handle == _handle3) label = _label3;
            else if (handle == _handle4) label = _label4;
            if (label != null)
            {
                Canvas.SetLeft(label, x - label.Width / 2);
                Canvas.SetTop(label, y + handle.Height / 2 + 2);
            }

            // Position triangle and combo (if present via Tag)
            if (handle.Tag is LightHandleUi ui)
            {
                Canvas.SetLeft(ui.Triangle, x);
                Canvas.SetTop(ui.Triangle, y - handle.Height / 2 - 24);
                Canvas.SetLeft(ui.Combo, x + handle.Width / 2 + 6);
                Canvas.SetTop(ui.Combo, y - ui.Combo.ActualHeight / 2);
            }
        }

        private void StartMcpServer()
        {
            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add("http://localhost:5056/");
                _httpListener.Start();
            }
            catch
            {
                _httpListener = null;
                return;
            }

            _httpThread = new Thread(() =>
            {
                while (_httpListener != null && _httpListener.IsListening)
                {
                    try
                    {
                        var ctx = _httpListener.BeginGetContext(ar =>
                        {
                            HttpListenerContext? context = null;
                            try { context = _httpListener.EndGetContext(ar); } catch { }
                            if (context == null) return;
                            HandleHttp(context);
                        }, null);
                        ctx.AsyncWaitHandle.WaitOne();
                    }
                    catch { /* swallow and continue */ }
                }
            }) { IsBackground = true };
            _httpThread.Start();
        }

        private void HandleHttp(HttpListenerContext context)
        {
            var req = context.Request;
            var res = context.Response;
            try
            {
                if (req.HttpMethod == "GET" && req.Url != null && req.Url.AbsolutePath == "/state")
                {
                    var state = CaptureState();
                    var json = JsonSerializer.Serialize(state);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    res.ContentType = "application/json";
                    res.OutputStream.Write(bytes, 0, bytes.Length);
                    res.StatusCode = 200;
                }
                else if (req.HttpMethod == "POST" && req.Url != null && req.Url.AbsolutePath == "/sphere")
                {
                    using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                    var body = reader.ReadToEnd();
                    var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("radius", out var rEl))
                    {
                        var r = rEl.GetDouble();
                        Dispatcher.Invoke(() => { SphereSizeSlider!.Value = Math.Max(SphereSizeSlider.Minimum, Math.Min(SphereSizeSlider.Maximum, r)); });
                    }
                    if (doc.RootElement.TryGetProperty("texture", out var tEl))
                    {
                        var t = tEl.GetString() ?? "Grid";
                        Dispatcher.Invoke(() =>
                        {
                            foreach (var obj in TextureComboBox!.Items)
                            {
                                if (obj is ComboBoxItem cbi && string.Equals(cbi.Content?.ToString(), t, StringComparison.OrdinalIgnoreCase))
                                {
                                    TextureComboBox.SelectedItem = cbi;
                                    break;
                                }
                            }
                        });
                    }
                    res.StatusCode = 204;
                }
                else if (req.HttpMethod == "POST" && req.Url != null && req.Url.AbsolutePath.StartsWith("/light/", StringComparison.OrdinalIgnoreCase))
                {
                    var seg = req.Url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (seg.Length == 2 && int.TryParse(seg[1], out int idx) && idx >= 1 && idx <= 4)
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                        var body = reader.ReadToEnd();
                        var doc = JsonDocument.Parse(body);
                        double? x = doc.RootElement.TryGetProperty("x", out var xEl) ? xEl.GetDouble() : null;
                        double? y = doc.RootElement.TryGetProperty("y", out var yEl) ? yEl.GetDouble() : null;
                        double? z = doc.RootElement.TryGetProperty("z", out var zEl) ? zEl.GetDouble() : null;
                        Dispatcher.Invoke(() =>
                        {
                            var light = idx switch { 1 => _draggableLight1, 2 => _draggableLight2, 3 => _draggableLight3, 4 => _draggableLight4, _ => null };
                            if (light != null)
                            {
                                var p = light.Position;
                                light.Position = new Point3D(x ?? p.X, y ?? p.Y, z ?? p.Z);
                                var handle = idx switch { 1 => _handle1, 2 => _handle2, 3 => _handle3, 4 => _handle4, _ => null };
                                if (handle != null) PositionHandle(handle, light.Position);
                            }
                        });
                        res.StatusCode = 204;
                    }
                    else
                    {
                        res.StatusCode = 400;
                    }
                }
                else
                {
                    res.StatusCode = 404;
                }
            }
            catch
            {
                res.StatusCode = 500;
            }
            finally
            {
                try { res.OutputStream.Close(); } catch { }
            }
        }

        private object CaptureState()
        {
            var lights = new[] { _draggableLight1, _draggableLight2, _draggableLight3, _draggableLight4 };
            var arr = lights.Select((l, i) => new { index = i + 1, x = l?.Position.X ?? 0, y = l?.Position.Y ?? 0, z = l?.Position.Z ?? 0 }).ToArray();
            return new
            {
                radius = _sphereRadius,
                texture = _currentTextureName,
                lights = arr
            };
        }

        private static MeshGeometry3D CreateSphereMesh(double radius, int slices, int stacks)
        {
            var mesh = new MeshGeometry3D();
            var positions = new List<Point3D>();
            var normals = new List<Vector3D>();
            var tex = new List<Point>();
            var indices = new List<int>();

            for (int stack = 0; stack <= stacks; stack++)
            {
                double phi = Math.PI * stack / stacks; // 0..PI
                double y = Math.Cos(phi);
                double r = Math.Sin(phi);
                for (int slice = 0; slice <= slices; slice++)
                {
                    double theta = 2 * Math.PI * slice / slices; // 0..2PI
                    double x = r * Math.Cos(theta);
                    double z = r * Math.Sin(theta);
                    var normal = new Vector3D(x, y, z);
                    normal.Normalize();
                    positions.Add(new Point3D(radius * x, radius * y, radius * z));
                    normals.Add(normal);
                    tex.Add(new Point((double)slice / slices, 1.0 - (double)stack / stacks));
                }
            }

            int stride = slices + 1;
            for (int stack = 0; stack < stacks; stack++)
            {
                for (int slice = 0; slice < slices; slice++)
                {
                    int a = stack * stride + slice;
                    int b = (stack + 1) * stride + slice;
                    int c = a + 1;
                    int d = b + 1;
                    indices.Add(a); indices.Add(b); indices.Add(c);
                    indices.Add(c); indices.Add(b); indices.Add(d);
                }
            }

            mesh.Positions = new Point3DCollection(positions);
            mesh.Normals = new Vector3DCollection(normals);
            mesh.TriangleIndices = new Int32Collection(indices);
            mesh.TextureCoordinates = new PointCollection(tex);
            return mesh;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetupBrushes();
            Setup3DScene();
            SetupLights();
            SetupHandles();
            ApplyTintAndSetMaterial();
            StartAnimation();
        }

        private void SpinSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _spinSpeedBase = e.NewValue;
        }

    // Dial now uses 11 discrete directions around the circle; we map pointer angle to a unit vector
    // Slots: 0..10 around 360 degrees
    private static readonly double[] ElevenAnglesDeg = Enumerable.Range(0, 11).Select(i => i * (360.0 / 11.0)).ToArray();

        private void SphereColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_sphereMaterial == null) return;
            ApplyTintAndSetMaterial();
        }

        private void ApplyTintAndSetMaterial()
        {
            if (_sphereMaterial == null || _baseTextureBrush == null) return;
            var text = (SphereColorComboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Green";
            var color = text switch
            {
                "Blue" => Colors.SteelBlue,
                "Red" => Colors.IndianRed,
                "Purple" => Colors.MediumPurple,
                "Gray" => Colors.Gray,
                "White" => Colors.WhiteSmoke,
                _ => Colors.SeaGreen,
            };
            // Compose: base pattern + translucent color overlay
            var overlay = new GeometryDrawing(new SolidColorBrush(color) { Opacity = 0.25 }, null, new RectangleGeometry(new Rect(0, 0, 100, 100)));
            var group = new DrawingGroup();
            // Keep the base drawing first so tiling aligns
            group.Children.Add(_baseTextureBrush.Drawing);
            group.Children.Add(overlay);
            var brush = new DrawingBrush(group)
            {
                TileMode = _baseTextureBrush.TileMode,
                Viewport = _baseTextureBrush.Viewport,
                ViewportUnits = _baseTextureBrush.ViewportUnits,
                Viewbox = _baseTextureBrush.Viewbox,
                ViewboxUnits = _baseTextureBrush.ViewboxUnits
            };
            _sphereMaterial.Brush = brush;
        }

        private static Color ParseNamedColor(string name)
        {
            // Map a handful of known names to Colors; default to White
            return name switch
            {
                "Red" => Colors.Red,
                "Orange" => Colors.Orange,
                "Yellow" => Colors.Yellow,
                "Lime" => Colors.Lime,
                "Cyan" => Colors.Cyan,
                "DeepSkyBlue" => Colors.DeepSkyBlue,
                "Blue" => Colors.Blue,
                "Magenta" => Colors.Magenta,
                "Gold" => Colors.Gold,
                "White" => Colors.White,
                _ => Colors.White
            };
        }

    // Removed old top-level light color combo handlers; per-handle combos are used instead.

        private void LightHandlesCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateHandlePositions();
        private void LightHandlesCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(this.LightHandlesCanvas!);
            // start dragging dial if clicked
            if (IsOverSpinDial(pos)) { _dialDragging = true; this.LightHandlesCanvas!.CaptureMouse(); return; }
            if (_handle1 != null && _handle1.IsMouseOver) { _dragging1 = true; _lastMousePos = pos; this.LightHandlesCanvas!.CaptureMouse(); }
            else if (_handle2 != null && _handle2.IsMouseOver) { _dragging2 = true; _lastMousePos = pos; this.LightHandlesCanvas!.CaptureMouse(); }
            else if (_handle3 != null && _handle3.IsMouseOver) { _dragging3 = true; _lastMousePos = pos; this.LightHandlesCanvas!.CaptureMouse(); }
            else if (_handle4 != null && _handle4.IsMouseOver) { _dragging4 = true; _lastMousePos = pos; this.LightHandlesCanvas!.CaptureMouse(); }
        }
        private void LightHandlesCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _dragging1 = _dragging2 = _dragging3 = _dragging4 = false;
            _dialDragging = false;
            this.LightHandlesCanvas!.ReleaseMouseCapture();
        }
    private void LightHandlesCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dialDragging)
            {
        var dialPos = e.GetPosition(this.LightHandlesCanvas!);
        UpdateSpinDialFromCanvasPos(dialPos);
                return;
            }
            if (!_dragging1 && !_dragging2 && !_dragging3 && !_dragging4) return;
            var pos = e.GetPosition(this.LightHandlesCanvas!);
            if (_dragging1 && _draggableLight1 != null && _handle1 != null)
            {
                var new3D = UnprojectFrom2D(pos, _draggableLight1.Position);
                _draggableLight1.Position = new3D;
                PositionHandle(_handle1, new3D);
            }
            else if (_dragging2 && _draggableLight2 != null && _handle2 != null)
            {
                var new3D = UnprojectFrom2D(pos, _draggableLight2.Position);
                _draggableLight2.Position = new3D;
                PositionHandle(_handle2, new3D);
            }
            else if (_dragging3 && _draggableLight3 != null && _handle3 != null)
            {
                var new3D = UnprojectFrom2D(pos, _draggableLight3.Position);
                _draggableLight3.Position = new3D;
                PositionHandle(_handle3, new3D);
            }
            else if (_dragging4 && _draggableLight4 != null && _handle4 != null)
            {
                var new3D = UnprojectFrom2D(pos, _draggableLight4.Position);
                _draggableLight4.Position = new3D;
                PositionHandle(_handle4, new3D);
            }
        }

        private void LightHandlesCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Determine which light to adjust: prefer currently dragged one, else hovered handle
            int idx = 0;
            if (_dragging1) idx = 1; else if (_dragging2) idx = 2; else if (_dragging3) idx = 3; else if (_dragging4) idx = 4;
            if (idx == 0)
            {
                if (_handle1 != null && _handle1.IsMouseOver) idx = 1;
                else if (_handle2 != null && _handle2.IsMouseOver) idx = 2;
                else if (_handle3 != null && _handle3.IsMouseOver) idx = 3;
                else if (_handle4 != null && _handle4.IsMouseOver) idx = 4;
            }
            if (idx == 0) return;

            double step = 0.3; // z units per wheel notch
            double delta = Math.Sign(e.Delta) * step;
            var (light, handle) = idx switch
            {
                1 => (_draggableLight1, _handle1),
                2 => (_draggableLight2, _handle2),
                3 => (_draggableLight3, _handle3),
                4 => (_draggableLight4, _handle4),
                _ => (null, null)
            };
            if (light == null || handle == null) return;
            var p = light.Position;
            double newZ = Math.Clamp(p.Z + delta, -10.0, 10.0);
            light.Position = new Point3D(p.X, p.Y, newZ);
            // Refresh handle tooltip and z-label overlay
            handle.ToolTip = $"Z: {newZ:F2}";
            PositionHandle(handle, light.Position);
            e.Handled = true;
        }

        private void UpdateHandlePositions()
        {
            if (_handle1 != null && _draggableLight1 != null)
                PositionHandle(_handle1, _draggableLight1.Position);
            if (_handle2 != null && _draggableLight2 != null)
                PositionHandle(_handle2, _draggableLight2.Position);
            if (_handle3 != null && _draggableLight3 != null)
                PositionHandle(_handle3, _draggableLight3.Position);
            if (_handle4 != null && _draggableLight4 != null)
                PositionHandle(_handle4, _draggableLight4.Position);

            // Position the instruction at the top center
            if (_zInstruction != null && this.LightHandlesCanvas != null)
            {
                double w = this.LightHandlesCanvas.ActualWidth;
                _zInstruction.Width = w;
                Canvas.SetLeft(_zInstruction, 0);
                Canvas.SetTop(_zInstruction, 2);
            }
        }

        private bool IsOverSpinDial(Point canvasPos)
        {
            // Project spin dial's center relative to the LightHandlesCanvas (approximate using top toolbar height ~60)
            // Since dial is in toolbar, not canvas, we only allow dragging via the dial itself; here we return false.
            return false;
        }

        private void UpdateSpinDialFromCanvasPos(Point canvasPos)
        {
            // No-op because we’re not mapping dial drag on canvas; dial itself handles drag in toolbar events.
        }

        private void SpinDial_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var p = e.GetPosition((IInputElement)sender);
            UpdateSpinDirectionFromDialPoint(p);
            ((UIElement)sender).CaptureMouse();
        }
        private void SpinDial_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            var p = e.GetPosition((IInputElement)sender);
            UpdateSpinDirectionFromDialPoint(p);
        }
        private void SpinDial_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)sender).ReleaseMouseCapture();
        }

        private RotateTransform? GetSpinDialRotate()
        {
            if (this.FindName("SpinDialPointer") is Polygon poly && poly.RenderTransform is RotateTransform rt)
            {
                return rt;
            }
            return null;
        }

        private void UpdateSpinDirectionFromDialPoint(Point p)
        {
            // Compute angle relative to dial center (32,32)
            var dx = p.X - 32; var dy = 32 - p.Y; // y-up
            var angDeg = Math.Atan2(dy, dx) * 180 / Math.PI; // -180..180
            var r = GetSpinDialRotate();
            // Point towards snapped direction
            double snapped = SnapAngleTo11(angDeg);
            if (r != null)
            {
                r.Angle = 90 - snapped; // convert to UI orientation (0 at right)
            }
            var rad = snapped * Math.PI / 180.0;
            _spinDirVec = new Vector2D(Math.Cos(rad), Math.Sin(rad));
        }

        private static double SnapAngleTo11(double deg)
        {
            // Normalize to 0..360
            double a = (deg % 360 + 360) % 360;
            // Find nearest slot
            double slotSize = 360.0 / 11.0;
            int slot = (int)Math.Round(a / slotSize);
            if (slot == 11) slot = 0;
            return slot * slotSize;
        }

        private readonly struct Vector2D
        {
            public readonly double X;
            public readonly double Y;
            public Vector2D(double x, double y) { X = x; Y = y; }
        }
        // ...existing code for MainWindow class (3D setup, MCP HTTP server, etc.)...
    }
}