
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
    private Ellipse _handle1 = null!;
    private Ellipse _handle2 = null!;
    private Ellipse _handle3 = null!;
    private Ellipse _handle4 = null!;
    private bool _dragging1 = false, _dragging2 = false, _dragging3 = false, _dragging4 = false;
    private PointLight _viewerLight = null!;
    private Point _lastMousePos;
    private AxisAngleRotation3D? _rotation;
    private double _sphereRadius = 2.1;
    private double _canvasSize = 700;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        LightHandlesCanvas.SizeChanged += LightHandlesCanvas_SizeChanged;
        LightHandlesCanvas.MouseLeftButtonDown += LightHandlesCanvas_MouseLeftButtonDown;
        LightHandlesCanvas.MouseLeftButtonUp += LightHandlesCanvas_MouseLeftButtonUp;
        LightHandlesCanvas.MouseMove += LightHandlesCanvas_MouseMove;
    }


    private void SphereSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _sphereRadius = e.NewValue;
        if (_sphereModel != null)
        {
            _sphereModel.Geometry = CreateSphereMesh(_sphereRadius, 48, 32);
        }
        UpdateHandlePositions();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SetupBrushes();
        Setup3DScene();
        SetupLights();
        SetupHandles();
        StartAnimation();
    }

    private void SetupBrushes()
    {
        // Grid DrawingBrush over green
        var grid = new DrawingGroup();
        double cell = 0.12; // 12% of sphere diameter
        // Green background
        grid.Children.Add(new GeometryDrawing(Brushes.Green, null, new RectangleGeometry(new Rect(0, 0, 1, 1))));
        // White grid lines
        for (double i = 0; i <= 1; i += cell)
        {
            grid.Children.Add(new GeometryDrawing(null, new Pen(Brushes.White, 0.01), new LineGeometry(new Point(i, 0), new Point(i, 1))));
            grid.Children.Add(new GeometryDrawing(null, new Pen(Brushes.White, 0.01), new LineGeometry(new Point(0, i), new Point(1, i))));
        }
        _gridBrush = new DrawingBrush(grid) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, cell, cell), ViewportUnits = BrushMappingMode.RelativeToBoundingBox, Stretch = Stretch.Fill };

        // Octagon grid DrawingBrush over green
        var oct = new DrawingGroup();
        oct.Children.Add(new GeometryDrawing(Brushes.Green, null, new RectangleGeometry(new Rect(0, 0, 1, 1))));
        for (double y = 0; y < 1; y += cell)
        {
            for (double x = 0; x < 1; x += cell)
            {
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    double cx = x + cell / 2, cy = y + cell / 2, r = cell * 0.4;
                    ctx.BeginFigure(new Point(cx + r, cy), true, true);
                    for (int k = 1; k < 8; k++)
                    {
                        double angle = Math.PI / 4 * k;
                        ctx.LineTo(new Point(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle)), true, false);
                    }
                }
                oct.Children.Add(new GeometryDrawing(null, new Pen(Brushes.White, 0.01), geo));
            }
        }
        _octagonBrush = new DrawingBrush(oct) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, cell, cell), ViewportUnits = BrushMappingMode.RelativeToBoundingBox, Stretch = Stretch.Fill };
    }

    private void Setup3DScene()
    {
    MainViewport.Children.Clear();
        _sceneGroup = new Model3DGroup();
    _sphereMaterial = new DiffuseMaterial();
        _sphereModel = new GeometryModel3D(CreateSphereMesh(_sphereRadius, 48, 32), _sphereMaterial);
        _sceneGroup.Children.Add(_sphereModel);
        var modelVisual = new ModelVisual3D { Content = _sceneGroup };
    MainViewport.Children.Add(modelVisual);
    MainViewport.Camera = new PerspectiveCamera(new Point3D(0, 0, 10), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 45);
        _rotation = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
        _sphereModel.Transform = new RotateTransform3D(_rotation);
        UpdateTexture();
    }

    private MeshGeometry3D CreateSphereMesh(double radius, int tDiv, int pDiv)
    {
        var mesh = new MeshGeometry3D();
        for (int pi = 0; pi <= pDiv; pi++)
        {
            double phi = Math.PI * pi / pDiv;
            for (int ti = 0; ti <= tDiv; ti++)
            {
                double theta = 2 * Math.PI * ti / tDiv;
                double x = radius * Math.Sin(phi) * Math.Cos(theta);
                double y = radius * Math.Cos(phi);
                double z = radius * Math.Sin(phi) * Math.Sin(theta);
                mesh.Positions.Add(new Point3D(x, y, z));
                mesh.Normals.Add(new Vector3D(x, y, z));
                mesh.TextureCoordinates.Add(new Point((double)ti / tDiv, (double)pi / pDiv));
            }
        }
        for (int pi = 0; pi < pDiv; pi++)
        {
            for (int ti = 0; ti < tDiv; ti++)
            {
                int a = pi * (tDiv + 1) + ti;
                int b = a + tDiv + 1;
                mesh.TriangleIndices.Add(a); mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(a + 1);
                mesh.TriangleIndices.Add(a + 1); mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(b + 1);
            }
        }
        return mesh;
    }

    private void SetupLights()
    {
    _sceneGroup!.Children.Remove(_fixedLight);
    _sceneGroup!.Children.Remove(_draggableLight1);
    _sceneGroup!.Children.Remove(_draggableLight2);
    _sceneGroup!.Children.Remove(_draggableLight3);
    _sceneGroup!.Children.Remove(_draggableLight4);
    _sceneGroup!.Children.Remove(_viewerLight);
    double r = _sphereRadius + 0.5;
    _fixedLight = new PointLight(Color.FromScRgb(1000f, 1, 1, 1), new Point3D(0, r, r));
    _draggableLight1 = new PointLight(Color.FromScRgb(1000f, 1, 1, 0), new Point3D(-r, 0, r));
    _draggableLight2 = new PointLight(Color.FromScRgb(1000f, 0, 1, 1), new Point3D(r, 0, r));
    _draggableLight3 = new PointLight(Color.FromScRgb(1000f, 1, 0, 1), new Point3D(0, -r, r));
    _draggableLight4 = new PointLight(Color.FromScRgb(1000f, 1, 1, 0.5f), new Point3D(0, 0, r + r));
    // Add a static white light at the camera's position (viewer perspective)
    var camera = MainViewport.Camera as PerspectiveCamera;
    if (camera != null)
    {
        _viewerLight = new PointLight(Color.FromScRgb(10000f, 1, 1, 1), camera.Position);
        _sceneGroup!.Children.Add(_viewerLight);
    }
    _sceneGroup!.Children.Add(_fixedLight);
    _sceneGroup!.Children.Add(_draggableLight1);
    _sceneGroup!.Children.Add(_draggableLight2);
    _sceneGroup!.Children.Add(_draggableLight3);
    _sceneGroup!.Children.Add(_draggableLight4);
    }

    private void SetupHandles()
    {
    LightHandlesCanvas.Children.Clear();
    _handle1 = CreateHandle(Colors.Yellow);
    _handle2 = CreateHandle(Colors.Cyan);
    _handle3 = CreateHandle(Colors.Magenta);
    _handle4 = CreateHandle(Colors.Orange);
    LightHandlesCanvas.Children.Add(_handle1);
    LightHandlesCanvas.Children.Add(_handle2);
    LightHandlesCanvas.Children.Add(_handle3);
    LightHandlesCanvas.Children.Add(_handle4);
    UpdateHandlePositions();
    }

    private Ellipse CreateHandle(Color color)
    {
        return new Ellipse
        {
            Width = 28,
            Height = 28,
            Fill = new SolidColorBrush(Color.FromArgb(128, color.R, color.G, color.B)),
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 2,
            Cursor = Cursors.Hand
        };
    }

    private void UpdateHandlePositions()
    {
        // Project 3D light positions to 2D canvas
        if (_handle1 != null && _draggableLight1 != null)
            PositionHandle(_handle1, _draggableLight1.Position);
        if (_handle2 != null && _draggableLight2 != null)
            PositionHandle(_handle2, _draggableLight2.Position);
        if (_handle3 != null && _draggableLight3 != null)
            PositionHandle(_handle3, _draggableLight3.Position);
        if (_handle4 != null && _draggableLight4 != null)
            PositionHandle(_handle4, _draggableLight4.Position);
    }

    private void PositionHandle(Ellipse handle, Point3D pos)
    {
        var cam = MainViewport.Camera as PerspectiveCamera;
        if (cam == null) return;
        var pt = ProjectTo2D(pos, cam);
        Canvas.SetLeft(handle, pt.X - handle.Width / 2);
        Canvas.SetTop(handle, pt.Y - handle.Height / 2);
    }

    private Point ProjectTo2D(Point3D pt, PerspectiveCamera cam)
    {
        // Project 3D to 2D, scale distance by 3x per pixel, and also by sphere radius
        double baseRadius = 3.0; // default sphere radius
        double scale = LightHandlesCanvas.ActualWidth / (baseRadius * 2.5) / 3.0 * (_sphereRadius / baseRadius);
        double x = pt.X * scale + LightHandlesCanvas.ActualWidth / 2;
        double y = -pt.Y * scale + LightHandlesCanvas.ActualHeight / 2;
        return new Point(x, y);
    }

    private Point3D UnprojectFrom2D(Point pt, Point3D ref3D)
    {
        double baseRadius = 3.0;
        double scale = LightHandlesCanvas.ActualWidth / (baseRadius * 2.5) / 3.0 * (_sphereRadius / baseRadius);
        double x = (pt.X - LightHandlesCanvas.ActualWidth / 2) / scale;
        double y = -(pt.Y - LightHandlesCanvas.ActualHeight / 2) / scale;
        return new Point3D(x, y, ref3D.Z); // Keep Z fixed for simplicity
    }

    private void LightHandlesCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateHandlePositions();
    }

    private void LightHandlesCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
    var pos = e.GetPosition(LightHandlesCanvas);
    if (_handle1 != null && _handle1.IsMouseOver) { _dragging1 = true; _lastMousePos = pos; LightHandlesCanvas.CaptureMouse(); }
    else if (_handle2 != null && _handle2.IsMouseOver) { _dragging2 = true; _lastMousePos = pos; LightHandlesCanvas.CaptureMouse(); }
    else if (_handle3 != null && _handle3.IsMouseOver) { _dragging3 = true; _lastMousePos = pos; LightHandlesCanvas.CaptureMouse(); }
    else if (_handle4 != null && _handle4.IsMouseOver) { _dragging4 = true; _lastMousePos = pos; LightHandlesCanvas.CaptureMouse(); }
    }

    private void LightHandlesCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
    _dragging1 = _dragging2 = _dragging3 = _dragging4 = false;
    LightHandlesCanvas.ReleaseMouseCapture();
    }

    private void LightHandlesCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging1 && !_dragging2 && !_dragging3 && !_dragging4) return;
        var pos = e.GetPosition(LightHandlesCanvas);
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

    private void TextureComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateTexture();
    }

    private void UpdateTexture()
    {
            if (TextureComboBox.SelectedIndex == 0 && _sphereMaterial != null && _gridBrush != null)
                _sphereMaterial.Brush = _gridBrush;
            else if (_sphereMaterial != null && _octagonBrush != null)
                _sphereMaterial.Brush = _octagonBrush;
    }

    private void StartAnimation()
    {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(8)))
        {
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
        };
            if (_rotation != null)
                _rotation.BeginAnimation(AxisAngleRotation3D.AngleProperty, anim);
    }
}
}