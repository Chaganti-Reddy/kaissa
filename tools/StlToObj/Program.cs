using System.Globalization;
using System.Text;

// Converts binary STL chess pieces into OBJ (which Unity imports natively), rotated Z-up -> Y-up,
// scaled so each piece fits a board square, and seated with its base at y = 0.
//   dotnet run --project tools/StlToObj -- <inputDir> <outputDir>
// Expects <inputDir>/{pawn,knight,bishop,rook,queen,king}.stl.

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: StlToObj <inputDir> <outputDir>");
    return 1;
}

string inputDir = args[0];
string outputDir = args[1];
Directory.CreateDirectory(outputDir);

var names = new (string file, string obj)[]
{
    ("pawn", "Pawn"), ("knight", "Knight"), ("bishop", "Bishop"),
    ("rook", "Rook"), ("queen", "Queen"), ("king", "King"),
};

foreach (var (file, obj) in names)
{
    var path = Path.Combine(inputDir, $"{file}.stl");
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"missing {path}");
        continue;
    }

    var tris = ReadBinaryStl(path);
    WriteObj(Path.Combine(outputDir, $"{obj}.obj"), tris);
    Console.WriteLine($"{file}.stl -> {obj}.obj ({tris.Count} triangles)");
}

return 0;

static List<(Vec a, Vec b, Vec c)> ReadBinaryStl(string path)
{
    using var reader = new BinaryReader(File.OpenRead(path));
    reader.ReadBytes(80); // header
    uint count = reader.ReadUInt32();

    var tris = new List<(Vec, Vec, Vec)>((int)count);
    for (uint i = 0; i < count; i++)
    {
        reader.ReadSingle(); reader.ReadSingle(); reader.ReadSingle(); // facet normal (ignored)
        var a = ReadVertex(reader);
        var b = ReadVertex(reader);
        var c = ReadVertex(reader);
        reader.ReadUInt16(); // attribute byte count
        tris.Add((a, b, c));
    }
    return tris;
}

// Z-up (print) -> Y-up (Unity); a proper rotation so winding/normals stay outward.
static Vec ReadVertex(BinaryReader r)
{
    float x = r.ReadSingle();
    float y = r.ReadSingle();
    float z = r.ReadSingle();
    return new Vec(x, z, -y);
}

static void WriteObj(string path, List<(Vec a, Vec b, Vec c)> tris)
{
    float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
    float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
    foreach (var (a, b, c) in tris)
        foreach (var v in new[] { a, b, c })
        {
            minX = Math.Min(minX, v.X); maxX = Math.Max(maxX, v.X);
            minY = Math.Min(minY, v.Y); maxY = Math.Max(maxY, v.Y);
            minZ = Math.Min(minZ, v.Z); maxZ = Math.Max(maxZ, v.Z);
        }

    float footprint = Math.Max(maxX - minX, maxZ - minZ);
    float scale = footprint > 0 ? 0.72f / footprint : 1f;
    float cx = (minX + maxX) / 2f, cz = (minZ + maxZ) / 2f, baseY = minY;

    Vec Fix(Vec v) => new((v.X - cx) * scale, (v.Y - baseY) * scale, (v.Z - cz) * scale);

    var verts = new StringBuilder();
    var normals = new StringBuilder();
    var faces = new StringBuilder();
    int vi = 1;
    int ni = 1;
    var ci = CultureInfo.InvariantCulture;

    foreach (var (a0, b0, c0) in tris)
    {
        Vec a = Fix(a0), b = Fix(b0), c = Fix(c0);
        Vec n = Normal(a, b, c);
        verts.Append("v ").Append(a.X.ToString(ci)).Append(' ').Append(a.Y.ToString(ci)).Append(' ').Append(a.Z.ToString(ci)).Append('\n');
        verts.Append("v ").Append(b.X.ToString(ci)).Append(' ').Append(b.Y.ToString(ci)).Append(' ').Append(b.Z.ToString(ci)).Append('\n');
        verts.Append("v ").Append(c.X.ToString(ci)).Append(' ').Append(c.Y.ToString(ci)).Append(' ').Append(c.Z.ToString(ci)).Append('\n');
        normals.Append("vn ").Append(n.X.ToString(ci)).Append(' ').Append(n.Y.ToString(ci)).Append(' ').Append(n.Z.ToString(ci)).Append('\n');
        faces.Append($"f {vi}//{ni} {vi + 1}//{ni} {vi + 2}//{ni}\n");
        vi += 3;
        ni += 1;
    }

    File.WriteAllText(path, verts.ToString() + normals + faces);
}

static Vec Normal(Vec a, Vec b, Vec c)
{
    Vec u = new(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
    Vec w = new(c.X - a.X, c.Y - a.Y, c.Z - a.Z);
    Vec n = new(u.Y * w.Z - u.Z * w.Y, u.Z * w.X - u.X * w.Z, u.X * w.Y - u.Y * w.X);
    float len = MathF.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z);
    return len > 0 ? new Vec(n.X / len, n.Y / len, n.Z / len) : n;
}

readonly record struct Vec(float X, float Y, float Z);
