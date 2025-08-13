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
            _sphereMaterial.Brush = item.Contains("Octagon", StringComparison.OrdinalIgnoreCase) ? _octagonBrush : _gridBrush;
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
            _sphereMaterial = new DiffuseMaterial(_gridBrush);
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
            _draggableLight1 = new PointLight(Colors.Red, new Point3D(-4.5, 0, 3.5)) { Range = 120, ConstantAttenuation = 0.9, LinearAttenuation = 0.03 };
            _draggableLight2 = new PointLight(Colors.Lime, new Point3D(4.5, 0, 3.5)) { Range = 120, ConstantAttenuation = 0.9, LinearAttenuation = 0.03 };
            _draggableLight3 = new PointLight(Colors.DeepSkyBlue, new Point3D(0, 4.5, 3.5)) { Range = 120, ConstantAttenuation = 0.9, LinearAttenuation = 0.03 };
            _draggableLight4 = new PointLight(Colors.Gold, new Point3D(0, -4.5, 3.5)) { Range = 120, ConstantAttenuation = 0.9, LinearAttenuation = 0.03 };
            _sceneGroup.Children.Add(_draggableLight1);
            _sceneGroup.Children.Add(_draggableLight2);
            _sceneGroup.Children.Add(_draggableLight3);
            _sceneGroup.Children.Add(_draggableLight4);
        }

        private void SetupHandles()
        {
            _handle1 = MakeHandle(Colors.Red);
            _handle2 = MakeHandle(Colors.Lime);
            _handle3 = MakeHandle(Colors.DeepSkyBlue);
            _handle4 = MakeHandle(Colors.Gold);
            _zText1 = MakeZText();
            _zText2 = MakeZText();
            _zText3 = MakeZText();
            _zText4 = MakeZText();
            if (_zInstruction == null)
            {
                _zInstruction = new TextBlock
                {
                    Text = "Tip: Use mouse wheel over a handle to adjust Z (depth)",
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
            this.LightHandlesCanvas.Children.Add(_handle2);
            this.LightHandlesCanvas.Children.Add(_zText2);
            this.LightHandlesCanvas.Children.Add(_handle3);
            this.LightHandlesCanvas.Children.Add(_zText3);
            this.LightHandlesCanvas.Children.Add(_handle4);
            this.LightHandlesCanvas.Children.Add(_zText4);
            UpdateHandlePositions();
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
                    _rotation.Angle = (_rotation.Angle + 0.3) % 360.0;
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
            StartAnimation();
        }

        private void LightHandlesCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateHandlePositions();
        private void LightHandlesCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(this.LightHandlesCanvas!);
            if (_handle1 != null && _handle1.IsMouseOver) { _dragging1 = true; _lastMousePos = pos; this.LightHandlesCanvas!.CaptureMouse(); }
            else if (_handle2 != null && _handle2.IsMouseOver) { _dragging2 = true; _lastMousePos = pos; this.LightHandlesCanvas!.CaptureMouse(); }
            else if (_handle3 != null && _handle3.IsMouseOver) { _dragging3 = true; _lastMousePos = pos; this.LightHandlesCanvas!.CaptureMouse(); }
            else if (_handle4 != null && _handle4.IsMouseOver) { _dragging4 = true; _lastMousePos = pos; this.LightHandlesCanvas!.CaptureMouse(); }
        }
        private void LightHandlesCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _dragging1 = _dragging2 = _dragging3 = _dragging4 = false;
            this.LightHandlesCanvas!.ReleaseMouseCapture();
        }
        private void LightHandlesCanvas_MouseMove(object sender, MouseEventArgs e)
        {
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
        // ...existing code for MainWindow class (3D setup, MCP HTTP server, etc.)...
    }
}