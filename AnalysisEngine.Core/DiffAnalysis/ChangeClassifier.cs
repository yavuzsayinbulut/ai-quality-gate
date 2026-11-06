using System.Text.RegularExpressions;
using AnalysisEngine.Core.Models;

namespace AnalysisEngine.Core.DiffAnalysis;

/// <summary>
/// Classifies parsed diff hunks into typed ChangeAtom records.
/// </summary>
public class ChangeClassifier
{
    // ═══ C# REGEX PATTERNS ═══
    private static readonly Regex PropertyDeclRegex = new(
        @"(?:public\s+)?(\w+\??)\s+(\w+)\s*[{,;)\n]",
        RegexOptions.Compiled);

    private static readonly Regex MethodDeclRegex = new(
        @"(public|private|protected|internal)\s+(static\s+)?(async\s+)?[\w<>,\?\[\]\s]+\s+(\w+)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex ThrowRegex = new(
        @"throw\s+new\s+(\w+Exception)",
        RegexOptions.Compiled);

    private static readonly Regex ValidationRegex = new(
        @"(\.MaxLength|\.MinLength|\.GreaterThan|\.LessThan|\.NotEmpty|\.Must|\.When|MaximumLength|MinimumLength|Range\(|Required|StringLength)",
        RegexOptions.Compiled);

    private static readonly Regex ArithmeticRegex = new(
        @"[\+\-\*\/]\s*=|[\+\-\*\/]\s+\w+|Math\.\w+|decimal\.Round|\.Round\(",
        RegexOptions.Compiled);

    private static readonly Regex ReturnNullRegex = new(
        @"return\s+null\s*;|=>\s*null\s*;",
        RegexOptions.Compiled);

    // ═══ JAVA REGEX PATTERNS ═══
    // Java field: private BigDecimal amount; veya private String cardNumber;
    private static readonly Regex JavaFieldDeclRegex = new(
        @"(?:private|protected|public)\s+(?:final\s+)?(\w+(?:<[\w<>,\s]+>)?)\s+(\w+)\s*[;=]",
        RegexOptions.Compiled);

    // Java method: public ResponseEntity<PaymentModel> initPayment(Request req)
    private static readonly Regex JavaMethodDeclRegex = new(
        @"(public|private|protected)\s+(?:static\s+)?(?:final\s+)?(?:synchronized\s+)?[\w<>,\?\[\]\s]+\s+(\w+)\s*\(",
        RegexOptions.Compiled);

    // Java throw: throw new PaymentException("message")
    private static readonly Regex JavaThrowRegex = new(
        @"throw\s+new\s+(\w+(?:Exception|Error|Throwable))",
        RegexOptions.Compiled);

    // Java/Spring validation: @NotNull, @NotBlank, @Size, @Min, @Max, @Valid, @Positive
    private static readonly Regex JavaValidationRegex = new(
        @"@(NotNull|NotBlank|NotEmpty|Size|Min|Max|Pattern|Email|Valid|Positive|PositiveOrZero|Negative|DecimalMin|DecimalMax|Digits|Past|Future|AssertTrue|AssertFalse)",
        RegexOptions.Compiled);

    // Java arithmetic: BigDecimal operations, Math methods
    private static readonly Regex JavaArithmeticRegex = new(
        @"\.add\(|\.subtract\(|\.multiply\(|\.divide\(|\.setScale\(|BigDecimal\.ZERO|BigDecimal\.valueOf|Math\.\w+|\.compareTo\(",
        RegexOptions.Compiled);

    // Java return null
    private static readonly Regex JavaReturnNullRegex = new(
        @"return\s+null\s*;|return\s+Optional\.empty\(\)\s*;|return\s+ResponseEntity\.notFound\(\)",
        RegexOptions.Compiled);

    // Spring annotation changes: @GetMapping, @PostMapping, @RequestMapping, @Transactional
    private static readonly Regex SpringAnnotationRegex = new(
        @"@(GetMapping|PostMapping|PutMapping|DeleteMapping|PatchMapping|RequestMapping|Transactional|Cacheable|CacheEvict|Async|Scheduled|EventListener|RabbitListener|KafkaListener)\s*\(",
        RegexOptions.Compiled);

    // Lombok annotations: @Data, @Builder, @Getter, @Setter, @NoArgsConstructor
    private static readonly Regex LombokAnnotationRegex = new(
        @"@(Data|Builder|Getter|Setter|NoArgsConstructor|AllArgsConstructor|RequiredArgsConstructor|Value|ToString|EqualsAndHashCode|Slf4j|Log4j2)",
        RegexOptions.Compiled);

    // ═══ GO REGEX PATTERNS ═══
    private static readonly Regex GoFieldDeclRegex = new(
        @"(\w+)\s+([\w\.\*\[\]]+)\s*(?:`[^`]*`)?(?:\s*//.*)?$",
        RegexOptions.Compiled);

    private static readonly Regex GoFuncDeclRegex = new(
        @"func\s+(?:\(\s*\w+\s+\*?\w+\s*\)\s+)?(\w+)\s*\(",
        RegexOptions.Compiled);

    // ═══ JS/TS REGEX PATTERNS ═══
    private static readonly Regex JsPropertyDeclRegex = new(
        @"(?:(?:public|private|protected|readonly)\s+)?(\w+)\s*[?]?\s*:\s*(\w+(?:<[^>]+>)?)",
        RegexOptions.Compiled);

    private static readonly Regex JsFunctionDeclRegex = new(
        @"(?:export\s+)?(?:async\s+)?(?:function\s+(\w+)|(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s+)?\(?|(\w+)\s*\([^)]*\)\s*(?::\s*\w+)?\s*\{)",
        RegexOptions.Compiled);

    private static readonly Regex JsArrowFunctionRegex = new(
        @"(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s+)?\([^)]*\)\s*=>",
        RegexOptions.Compiled);

    /// <summary>
    /// Classify all changes from parsed diff files into ChangeAtom records.
    /// </summary>
    public List<ChangeAtom> Classify(List<DiffParser.DiffFile> files)
    {
        var atoms = new List<ChangeAtom>();

        foreach (var file in files)
        {
            var layer = DetermineLayer(file.FilePath);
            var isJava = file.FilePath.EndsWith(".java", StringComparison.OrdinalIgnoreCase);

            var isGo = file.FilePath.EndsWith(".go", StringComparison.OrdinalIgnoreCase);
            var isJs = file.FilePath.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
                       file.FilePath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
                       file.FilePath.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase) ||
                       file.FilePath.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase);

            if (isJava)
            {
                ClassifyJavaFields(file, layer, atoms);
                ClassifyJavaMethods(file, layer, atoms);
                ClassifyJavaExceptions(file, layer, atoms);
                ClassifyJavaValidation(file, layer, atoms);
                ClassifyJavaArithmetic(file, layer, atoms);
                ClassifyJavaReturnChanges(file, layer, atoms);
                ClassifyJavaMethodBodyChanges(file, layer, atoms);
                ClassifySpringAnnotationChanges(file, layer, atoms);
                ClassifyLombokChanges(file, layer, atoms);
            }
            else if (isGo)
            {
                ClassifyGoFields(file, layer, atoms);
                ClassifyGoFunctions(file, layer, atoms);
                ClassifyGoReturnChanges(file, layer, atoms);
            }
            else if (isJs)
            {
                ClassifyJsProperties(file, layer, atoms);
                ClassifyJsFunctions(file, layer, atoms);
                ClassifyJsReturnChanges(file, layer, atoms);
            }
            else
            {
                ClassifyProperties(file, layer, atoms);
                ClassifyMethods(file, layer, atoms);
                ClassifyExceptions(file, layer, atoms);
                ClassifyValidation(file, layer, atoms);
                ClassifyArithmetic(file, layer, atoms);
                ClassifyReturnChanges(file, layer, atoms);
                ClassifyMethodBodyChanges(file, layer, atoms);
            }

            // If file has only additions (new file)
            if (file.RemovedLines.Count == 0 && file.AddedLines.Count > 0 && file.Hunks.Count <= 1)
            {
                atoms.Add(new ChangeAtom(
                    ChangeType.FileAdded, file.FilePath, layer,
                    $"Yeni dosya eklendi: {Path.GetFileName(file.FilePath)}"));
            }

            // If file has MANY removals and no additions → dosya silindi veya buyuk parca silindi
            if (file.AddedLines.Count == 0 && file.RemovedLines.Count > 5 && file.Hunks.Count <= 1)
            {
                atoms.Add(new ChangeAtom(
                    ChangeType.FileRemoved, file.FilePath, layer,
                    $"Dosyadan buyuk parca silindi: {Path.GetFileName(file.FilePath)} ({file.RemovedLines.Count} satir)"));
            }
        }

        // If nothing was classified but there are changes, add generic Other
        if (atoms.Count == 0 && files.Count > 0)
        {
            foreach (var file in files)
            {
                atoms.Add(new ChangeAtom(
                    ChangeType.Other, file.FilePath, DetermineLayer(file.FilePath),
                    $"{Path.GetFileName(file.FilePath)} dosyasinda degisiklik tespit edildi"));
            }
        }

        return atoms;
    }

    private void ClassifyProperties(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        var removedProps = new Dictionary<string, string>(); // name -> type
        var addedProps = new Dictionary<string, string>();

        foreach (var line in file.RemovedLines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("/*")) continue; // Yorum satiri atla
            var match = PropertyDeclRegex.Match(trimmed);
            if (match.Success)
                removedProps[match.Groups[2].Value] = match.Groups[1].Value.Trim();
        }

        foreach (var line in file.AddedLines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("/*")) continue; // Yorum satiri atla
            var match = PropertyDeclRegex.Match(trimmed);
            if (match.Success)
                addedProps[match.Groups[2].Value] = match.Groups[1].Value.Trim();
        }

        // Properties that were removed but not re-added
        foreach (var prop in removedProps)
        {
            if (!addedProps.ContainsKey(prop.Key))
            {
                atoms.Add(new ChangeAtom(
                    ChangeType.PropertyRemoved, file.FilePath, layer,
                    $"'{prop.Key}' property'si silindi (tipi: {prop.Value})",
                    OldValue: $"{prop.Value} {prop.Key}"));
            }
            else if (addedProps[prop.Key] != prop.Value)
            {
                atoms.Add(new ChangeAtom(
                    ChangeType.PropertyTypeChanged, file.FilePath, layer,
                    $"'{prop.Key}' property'sinin tipi degisti: {prop.Value} → {addedProps[prop.Key]}",
                    OldValue: prop.Value, NewValue: addedProps[prop.Key]));
            }
        }

        // Properties that were added but didn't exist before
        foreach (var prop in addedProps)
        {
            if (!removedProps.ContainsKey(prop.Key))
            {
                atoms.Add(new ChangeAtom(
                    ChangeType.PropertyAdded, file.FilePath, layer,
                    $"'{prop.Key}' property'si eklendi (tipi: {prop.Value})",
                    NewValue: $"{prop.Value} {prop.Key}"));
            }
        }
    }

    private void ClassifyMethods(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        var removedMethods = new HashSet<string>();
        var addedMethods = new HashSet<string>();

        foreach (var line in file.RemovedLines.Where(l => !l.TrimStart().StartsWith("//")))
        {
            var match = MethodDeclRegex.Match(line);
            if (match.Success)
                removedMethods.Add(match.Groups[4].Value);
        }

        foreach (var line in file.AddedLines.Where(l => !l.TrimStart().StartsWith("//")))
        {
            var match = MethodDeclRegex.Match(line);
            if (match.Success)
                addedMethods.Add(match.Groups[4].Value);
        }

        foreach (var method in removedMethods)
        {
            if (!addedMethods.Contains(method))
            {
                atoms.Add(new ChangeAtom(
                    ChangeType.MethodRemoved, file.FilePath, layer,
                    $"'{method}' metodu silindi"));
            }
            else
            {
                atoms.Add(new ChangeAtom(
                    ChangeType.MethodSignatureChanged, file.FilePath, layer,
                    $"'{method}' metodunun imzasi degisti"));
            }
        }

        foreach (var method in addedMethods)
        {
            if (!removedMethods.Contains(method))
            {
                atoms.Add(new ChangeAtom(
                    ChangeType.MethodAdded, file.FilePath, layer,
                    $"'{method}' metodu eklendi"));
            }
        }
    }

    private void ClassifyExceptions(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        var removedExceptions = new HashSet<string>();
        var addedExceptions = new HashSet<string>();

        foreach (var line in file.RemovedLines)
        {
            var match = ThrowRegex.Match(line);
            if (match.Success)
                removedExceptions.Add(match.Groups[1].Value);
        }

        foreach (var line in file.AddedLines)
        {
            var match = ThrowRegex.Match(line);
            if (match.Success)
                addedExceptions.Add(match.Groups[1].Value);
        }

        foreach (var ex in addedExceptions.Except(removedExceptions))
        {
            atoms.Add(new ChangeAtom(
                ChangeType.ExceptionAdded, file.FilePath, layer,
                $"Yeni exception eklendi: {ex}"));
        }

        foreach (var ex in removedExceptions.Except(addedExceptions))
        {
            atoms.Add(new ChangeAtom(
                ChangeType.ExceptionRemoved, file.FilePath, layer,
                $"Exception kaldirildi: {ex}"));
        }
    }

    private void ClassifyValidation(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        bool removedValidation = file.RemovedLines.Any(l => ValidationRegex.IsMatch(l));
        bool addedValidation = file.AddedLines.Any(l => ValidationRegex.IsMatch(l));

        if (removedValidation || addedValidation)
        {
            atoms.Add(new ChangeAtom(
                ChangeType.ValidationChanged, file.FilePath, layer,
                "Validasyon kurallari degisti"));
        }
    }

    private void ClassifyArithmetic(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        bool hasArithmeticChange = file.RemovedLines.Any(l => ArithmeticRegex.IsMatch(l)) ||
                                   file.AddedLines.Any(l => ArithmeticRegex.IsMatch(l));

        if (hasArithmeticChange)
        {
            atoms.Add(new ChangeAtom(
                ChangeType.ArithmeticChanged, file.FilePath, layer,
                "Hesaplama/aritmetik mantigi degisti"));
        }
    }

    private void ClassifyReturnChanges(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        bool addedReturnNull = file.AddedLines.Any(l => ReturnNullRegex.IsMatch(l));
        bool removedReturnNull = file.RemovedLines.Any(l => ReturnNullRegex.IsMatch(l));

        if (addedReturnNull && !removedReturnNull)
        {
            // Hangi metodda return null eklendi?
            var methodName = "bilinmeyen metod";
            foreach (var line in file.AddedLines)
            {
                if (ReturnNullRegex.IsMatch(line)) break;
                var m = MethodDeclRegex.Match(line);
                if (m.Success) methodName = m.Groups[4].Value;
            }

            atoms.Add(new ChangeAtom(
                ChangeType.ReturnValueChanged, file.FilePath, layer,
                $"'{methodName}' metoduna 'return null' eklendi — donüs degeri degisti",
                OldValue: "normal return", NewValue: "return null"));
        }
    }

    private void ClassifyMethodBodyChanges(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        // Ayni metod hem silinen hem eklenen satirlarda varsa → govde degismis
        var removedMethodNames = new HashSet<string>();
        var addedMethodNames = new HashSet<string>();

        foreach (var line in file.RemovedLines.Where(l => !l.TrimStart().StartsWith("//")))
        {
            var match = MethodDeclRegex.Match(line);
            if (match.Success) removedMethodNames.Add(match.Groups[4].Value);
        }

        foreach (var line in file.AddedLines.Where(l => !l.TrimStart().StartsWith("//")))
        {
            var match = MethodDeclRegex.Match(line);
            if (match.Success) addedMethodNames.Add(match.Groups[4].Value);
        }

        // Metod imzasi ayni ama iceride degisiklik var (imza hem silindi hem eklendi)
        foreach (var method in removedMethodNames.Intersect(addedMethodNames))
        {
            // MethodSignatureChanged olarak zaten ekleniyorsa ekleme
            if (atoms.Any(a => a.Type == ChangeType.MethodSignatureChanged && a.Description.Contains($"'{method}'")))
                continue;

            // Sadece ciddi body degisiklikleri icin (3+ satir degisiklik)
            var removedCount = file.RemovedLines.Count;
            var addedCount = file.AddedLines.Count;
            if (removedCount + addedCount >= 6)
            {
                atoms.Add(new ChangeAtom(
                    ChangeType.MethodBodyChanged, file.FilePath, layer,
                    $"'{method}' metodunun govdesi degisti ({removedCount} satir silindi, {addedCount} satir eklendi)"));
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // GO CLASSIFICATION METHODS
    // ═══════════════════════════════════════════════════════════════

    private void ClassifyGoFields(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        var removedFields = new Dictionary<string, string>();
        var addedFields = new Dictionary<string, string>();

        foreach (var line in file.RemovedLines.Where(l => !l.TrimStart().StartsWith("//")))
        {
            var match = GoFieldDeclRegex.Match(line.Trim());
            if (match.Success && char.IsUpper(match.Groups[1].Value[0])) // exported fields only
                removedFields[match.Groups[1].Value] = match.Groups[2].Value;
        }
        foreach (var line in file.AddedLines.Where(l => !l.TrimStart().StartsWith("//")))
        {
            var match = GoFieldDeclRegex.Match(line.Trim());
            if (match.Success && char.IsUpper(match.Groups[1].Value[0]))
                addedFields[match.Groups[1].Value] = match.Groups[2].Value;
        }

        foreach (var f in removedFields)
        {
            if (!addedFields.ContainsKey(f.Key))
                atoms.Add(new ChangeAtom(ChangeType.PropertyRemoved, file.FilePath, layer,
                    $"'{f.Key}' alani silindi (tipi: {f.Value})", OldValue: $"{f.Value} {f.Key}"));
            else if (addedFields[f.Key] != f.Value)
                atoms.Add(new ChangeAtom(ChangeType.PropertyTypeChanged, file.FilePath, layer,
                    $"'{f.Key}' alaninin tipi degisti: {f.Value} → {addedFields[f.Key]}",
                    OldValue: f.Value, NewValue: addedFields[f.Key]));
        }
        foreach (var f in addedFields.Where(f => !removedFields.ContainsKey(f.Key)))
            atoms.Add(new ChangeAtom(ChangeType.PropertyAdded, file.FilePath, layer,
                $"'{f.Key}' alani eklendi (tipi: {f.Value})", NewValue: $"{f.Value} {f.Key}"));
    }

    private void ClassifyGoFunctions(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        var removed = new HashSet<string>();
        var added = new HashSet<string>();

        foreach (var line in file.RemovedLines.Where(l => !l.TrimStart().StartsWith("//")))
        { var m = GoFuncDeclRegex.Match(line); if (m.Success) removed.Add(m.Groups[1].Value); }
        foreach (var line in file.AddedLines.Where(l => !l.TrimStart().StartsWith("//")))
        { var m = GoFuncDeclRegex.Match(line); if (m.Success) added.Add(m.Groups[1].Value); }

        foreach (var f in removed.Except(added))
            atoms.Add(new ChangeAtom(ChangeType.MethodRemoved, file.FilePath, layer, $"'{f}' fonksiyonu silindi"));
        foreach (var f in added.Except(removed))
            atoms.Add(new ChangeAtom(ChangeType.MethodAdded, file.FilePath, layer, $"'{f}' fonksiyonu eklendi"));
        foreach (var f in removed.Intersect(added))
            atoms.Add(new ChangeAtom(ChangeType.MethodSignatureChanged, file.FilePath, layer, $"'{f}' fonksiyonunun imzasi degisti"));
    }

    private void ClassifyGoReturnChanges(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        bool addedNil = file.AddedLines.Any(l => Regex.IsMatch(l, @"return\s+nil\b"));
        bool removedNil = file.RemovedLines.Any(l => Regex.IsMatch(l, @"return\s+nil\b"));
        if (addedNil && !removedNil)
            atoms.Add(new ChangeAtom(ChangeType.ReturnValueChanged, file.FilePath, layer,
                "return nil eklendi — donüs degeri degisti", OldValue: "normal return", NewValue: "return nil"));
    }

    // ═══════════════════════════════════════════════════════════════
    // JS/TS CLASSIFICATION METHODS
    // ═══════════════════════════════════════════════════════════════

    private void ClassifyJsProperties(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        var removedProps = new Dictionary<string, string>();
        var addedProps = new Dictionary<string, string>();

        foreach (var line in file.RemovedLines.Where(l => !l.TrimStart().StartsWith("//")))
        {
            var match = JsPropertyDeclRegex.Match(line.Trim());
            if (match.Success) removedProps[match.Groups[1].Value] = match.Groups[2].Value;
        }
        foreach (var line in file.AddedLines.Where(l => !l.TrimStart().StartsWith("//")))
        {
            var match = JsPropertyDeclRegex.Match(line.Trim());
            if (match.Success) addedProps[match.Groups[1].Value] = match.Groups[2].Value;
        }

        foreach (var p in removedProps)
        {
            if (!addedProps.ContainsKey(p.Key))
                atoms.Add(new ChangeAtom(ChangeType.PropertyRemoved, file.FilePath, layer,
                    $"'{p.Key}' property'si silindi (tipi: {p.Value})", OldValue: $"{p.Value} {p.Key}"));
            else if (addedProps[p.Key] != p.Value)
                atoms.Add(new ChangeAtom(ChangeType.PropertyTypeChanged, file.FilePath, layer,
                    $"'{p.Key}' property'sinin tipi degisti: {p.Value} → {addedProps[p.Key]}",
                    OldValue: p.Value, NewValue: addedProps[p.Key]));
        }
        foreach (var p in addedProps.Where(p => !removedProps.ContainsKey(p.Key)))
            atoms.Add(new ChangeAtom(ChangeType.PropertyAdded, file.FilePath, layer,
                $"'{p.Key}' property'si eklendi (tipi: {p.Value})", NewValue: $"{p.Value} {p.Key}"));
    }

    private void ClassifyJsFunctions(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        var removed = new HashSet<string>();
        var added = new HashSet<string>();

        foreach (var line in file.RemovedLines.Where(l => !l.TrimStart().StartsWith("//")))
        {
            var m = JsFunctionDeclRegex.Match(line);
            if (m.Success) removed.Add(m.Groups[1].Value != "" ? m.Groups[1].Value : m.Groups[2].Value != "" ? m.Groups[2].Value : m.Groups[3].Value);
            var a = JsArrowFunctionRegex.Match(line);
            if (a.Success) removed.Add(a.Groups[1].Value);
        }
        foreach (var line in file.AddedLines.Where(l => !l.TrimStart().StartsWith("//")))
        {
            var m = JsFunctionDeclRegex.Match(line);
            if (m.Success) added.Add(m.Groups[1].Value != "" ? m.Groups[1].Value : m.Groups[2].Value != "" ? m.Groups[2].Value : m.Groups[3].Value);
            var a = JsArrowFunctionRegex.Match(line);
            if (a.Success) added.Add(a.Groups[1].Value);
        }

        removed.Remove(""); added.Remove("");
        foreach (var f in removed.Except(added))
            atoms.Add(new ChangeAtom(ChangeType.MethodRemoved, file.FilePath, layer, $"'{f}' fonksiyonu silindi"));
        foreach (var f in added.Except(removed))
            atoms.Add(new ChangeAtom(ChangeType.MethodAdded, file.FilePath, layer, $"'{f}' fonksiyonu eklendi"));
    }

    private void ClassifyJsReturnChanges(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        bool addedNull = file.AddedLines.Any(l => Regex.IsMatch(l, @"return\s+(null|undefined)\s*;"));
        bool removedNull = file.RemovedLines.Any(l => Regex.IsMatch(l, @"return\s+(null|undefined)\s*;"));
        if (addedNull && !removedNull)
            atoms.Add(new ChangeAtom(ChangeType.ReturnValueChanged, file.FilePath, layer,
                "return null/undefined eklendi — donüs degeri degisti", OldValue: "normal return", NewValue: "return null"));
    }

    // ═══════════════════════════════════════════════════════════════
    // JAVA CLASSIFICATION METHODS
    // ═══════════════════════════════════════════════════════════════

    private void ClassifyJavaFields(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        var removedFields = new Dictionary<string, string>(); // name -> type
        var addedFields = new Dictionary<string, string>();

        foreach (var line in file.RemovedLines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*")) continue;
            var match = JavaFieldDeclRegex.Match(trimmed);
            if (match.Success)
                removedFields[match.Groups[2].Value] = match.Groups[1].Value.Trim();
        }

        foreach (var line in file.AddedLines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*")) continue;
            var match = JavaFieldDeclRegex.Match(trimmed);
            if (match.Success)
                addedFields[match.Groups[2].Value] = match.Groups[1].Value.Trim();
        }

        foreach (var field in removedFields)
        {
            if (!addedFields.ContainsKey(field.Key))
            {
                atoms.Add(new ChangeAtom(
                    ChangeType.PropertyRemoved, file.FilePath, layer,
                    $"'{field.Key}' alani silindi (tipi: {field.Value})",
                    OldValue: $"{field.Value} {field.Key}"));
            }
            else if (addedFields[field.Key] != field.Value)
            {
                atoms.Add(new ChangeAtom(
                    ChangeType.PropertyTypeChanged, file.FilePath, layer,
                    $"'{field.Key}' alaninin tipi degisti: {field.Value} → {addedFields[field.Key]}",
                    OldValue: field.Value, NewValue: addedFields[field.Key]));
            }
        }

        foreach (var field in addedFields)
        {
            if (!removedFields.ContainsKey(field.Key))
            {
                atoms.Add(new ChangeAtom(
                    ChangeType.PropertyAdded, file.FilePath, layer,
                    $"'{field.Key}' alani eklendi (tipi: {field.Value})",
                    NewValue: $"{field.Value} {field.Key}"));
            }
        }
    }

    private void ClassifyJavaMethods(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        var removedMethods = new HashSet<string>();
        var addedMethods = new HashSet<string>();

        foreach (var line in file.RemovedLines.Where(l => !l.TrimStart().StartsWith("//")))
        {
            var match = JavaMethodDeclRegex.Match(line);
            if (match.Success) removedMethods.Add(match.Groups[2].Value);
        }

        foreach (var line in file.AddedLines.Where(l => !l.TrimStart().StartsWith("//")))
        {
            var match = JavaMethodDeclRegex.Match(line);
            if (match.Success) addedMethods.Add(match.Groups[2].Value);
        }

        foreach (var method in removedMethods)
        {
            if (!addedMethods.Contains(method))
                atoms.Add(new ChangeAtom(ChangeType.MethodRemoved, file.FilePath, layer, $"'{method}' metodu silindi"));
            else
                atoms.Add(new ChangeAtom(ChangeType.MethodSignatureChanged, file.FilePath, layer, $"'{method}' metodunun imzasi degisti"));
        }

        foreach (var method in addedMethods)
        {
            if (!removedMethods.Contains(method))
                atoms.Add(new ChangeAtom(ChangeType.MethodAdded, file.FilePath, layer, $"'{method}' metodu eklendi"));
        }
    }

    private void ClassifyJavaExceptions(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        var removedExceptions = new HashSet<string>();
        var addedExceptions = new HashSet<string>();

        foreach (var line in file.RemovedLines)
        {
            var match = JavaThrowRegex.Match(line);
            if (match.Success) removedExceptions.Add(match.Groups[1].Value);
        }

        foreach (var line in file.AddedLines)
        {
            var match = JavaThrowRegex.Match(line);
            if (match.Success) addedExceptions.Add(match.Groups[1].Value);
        }

        foreach (var ex in addedExceptions.Except(removedExceptions))
            atoms.Add(new ChangeAtom(ChangeType.ExceptionAdded, file.FilePath, layer, $"Yeni exception eklendi: {ex}"));

        foreach (var ex in removedExceptions.Except(addedExceptions))
            atoms.Add(new ChangeAtom(ChangeType.ExceptionRemoved, file.FilePath, layer, $"Exception kaldirildi: {ex}"));
    }

    private void ClassifyJavaValidation(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        bool removedValidation = file.RemovedLines.Any(l => JavaValidationRegex.IsMatch(l));
        bool addedValidation = file.AddedLines.Any(l => JavaValidationRegex.IsMatch(l));

        if (removedValidation || addedValidation)
        {
            var details = new List<string>();
            foreach (var line in file.AddedLines)
            {
                var m = JavaValidationRegex.Match(line);
                if (m.Success) details.Add($"@{m.Groups[1].Value}");
            }
            foreach (var line in file.RemovedLines)
            {
                var m = JavaValidationRegex.Match(line);
                if (m.Success && !details.Contains($"@{m.Groups[1].Value}"))
                    details.Add($"@{m.Groups[1].Value} (silindi)");
            }
            atoms.Add(new ChangeAtom(
                ChangeType.ValidationChanged, file.FilePath, layer,
                $"Java validasyon kurallari degisti: {string.Join(", ", details.Take(5))}"));
        }
    }

    private void ClassifyJavaArithmetic(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        bool hasArithmeticChange = file.RemovedLines.Any(l => JavaArithmeticRegex.IsMatch(l)) ||
                                   file.AddedLines.Any(l => JavaArithmeticRegex.IsMatch(l));

        if (hasArithmeticChange)
        {
            // BigDecimal precision degisimi tespiti
            var scaleChanged = file.AddedLines.Any(l => l.Contains(".setScale(")) ||
                              file.RemovedLines.Any(l => l.Contains(".setScale("));
            var desc = scaleChanged
                ? "BigDecimal hesaplama/precision degisti — setScale etkilendi"
                : "Java hesaplama/aritmetik mantigi degisti (BigDecimal)";
            atoms.Add(new ChangeAtom(ChangeType.ArithmeticChanged, file.FilePath, layer, desc));
        }
    }

    private void ClassifyJavaReturnChanges(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        bool addedReturnNull = file.AddedLines.Any(l => JavaReturnNullRegex.IsMatch(l));
        bool removedReturnNull = file.RemovedLines.Any(l => JavaReturnNullRegex.IsMatch(l));

        if (addedReturnNull && !removedReturnNull)
        {
            var methodName = "bilinmeyen metod";
            foreach (var line in file.AddedLines)
            {
                if (JavaReturnNullRegex.IsMatch(line)) break;
                var m = JavaMethodDeclRegex.Match(line);
                if (m.Success) methodName = m.Groups[2].Value;
            }

            var isOptionalEmpty = file.AddedLines.Any(l => l.Contains("Optional.empty()"));
            var desc = isOptionalEmpty
                ? $"'{methodName}' metoduna 'Optional.empty()' eklendi — bos deger donuyor"
                : $"'{methodName}' metoduna 'return null' eklendi — donüs degeri degisti";

            atoms.Add(new ChangeAtom(
                ChangeType.ReturnValueChanged, file.FilePath, layer, desc,
                OldValue: "normal return", NewValue: isOptionalEmpty ? "Optional.empty()" : "return null"));
        }
    }

    private void ClassifyJavaMethodBodyChanges(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        var removedMethodNames = new HashSet<string>();
        var addedMethodNames = new HashSet<string>();

        foreach (var line in file.RemovedLines.Where(l => !l.TrimStart().StartsWith("//")))
        {
            var match = JavaMethodDeclRegex.Match(line);
            if (match.Success) removedMethodNames.Add(match.Groups[2].Value);
        }

        foreach (var line in file.AddedLines.Where(l => !l.TrimStart().StartsWith("//")))
        {
            var match = JavaMethodDeclRegex.Match(line);
            if (match.Success) addedMethodNames.Add(match.Groups[2].Value);
        }

        foreach (var method in removedMethodNames.Intersect(addedMethodNames))
        {
            if (atoms.Any(a => a.Type == ChangeType.MethodSignatureChanged && a.Description.Contains($"'{method}'")))
                continue;

            var removedCount = file.RemovedLines.Count;
            var addedCount = file.AddedLines.Count;
            if (removedCount + addedCount >= 6)
            {
                atoms.Add(new ChangeAtom(
                    ChangeType.MethodBodyChanged, file.FilePath, layer,
                    $"'{method}' metodunun govdesi degisti ({removedCount} satir silindi, {addedCount} satir eklendi)"));
            }
        }
    }

    /// <summary>Spring endpoint annotation degisikliklerini tespit eder.</summary>
    private void ClassifySpringAnnotationChanges(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        var removedAnnotations = new HashSet<string>();
        var addedAnnotations = new HashSet<string>();

        foreach (var line in file.RemovedLines)
        {
            var match = SpringAnnotationRegex.Match(line);
            if (match.Success) removedAnnotations.Add(match.Groups[1].Value);
        }

        foreach (var line in file.AddedLines)
        {
            var match = SpringAnnotationRegex.Match(line);
            if (match.Success) addedAnnotations.Add(match.Groups[1].Value);
        }

        // Endpoint annotation silindi → API kirilmasi
        var removedEndpoints = removedAnnotations
            .Where(a => a.EndsWith("Mapping"))
            .Except(addedAnnotations)
            .ToList();

        if (removedEndpoints.Any())
        {
            atoms.Add(new ChangeAtom(
                ChangeType.MethodRemoved, file.FilePath, layer,
                $"Spring endpoint silindi: @{string.Join(", @", removedEndpoints)} — API kirilmasi!"));
        }

        // @Transactional eklendi/silindi
        if (addedAnnotations.Contains("Transactional") && !removedAnnotations.Contains("Transactional"))
        {
            atoms.Add(new ChangeAtom(
                ChangeType.Other, file.FilePath, layer,
                "@Transactional eklendi — transaction davranisi degisti"));
        }
        else if (removedAnnotations.Contains("Transactional") && !addedAnnotations.Contains("Transactional"))
        {
            atoms.Add(new ChangeAtom(
                ChangeType.Other, file.FilePath, layer,
                "@Transactional silindi — transaction korumasiz islem!"));
        }

        // @RabbitListener / @KafkaListener degisimi → mesajlasma etkisi
        var msgAnnotations = new[] { "RabbitListener", "KafkaListener", "EventListener" };
        foreach (var ann in msgAnnotations)
        {
            if (removedAnnotations.Contains(ann) && !addedAnnotations.Contains(ann))
                atoms.Add(new ChangeAtom(ChangeType.Other, file.FilePath, layer,
                    $"@{ann} silindi — mesaj tuketimi durdu!"));
            else if (addedAnnotations.Contains(ann) && !removedAnnotations.Contains(ann))
                atoms.Add(new ChangeAtom(ChangeType.Other, file.FilePath, layer,
                    $"@{ann} eklendi — yeni mesaj tuketimi"));
        }
    }

    /// <summary>Lombok annotation degisikliklerini tespit eder (model serialization etkisi).</summary>
    private void ClassifyLombokChanges(DiffParser.DiffFile file, FileLayer layer, List<ChangeAtom> atoms)
    {
        var removedLombok = new HashSet<string>();
        var addedLombok = new HashSet<string>();

        foreach (var line in file.RemovedLines)
        {
            var match = LombokAnnotationRegex.Match(line);
            if (match.Success) removedLombok.Add(match.Groups[1].Value);
        }

        foreach (var line in file.AddedLines)
        {
            var match = LombokAnnotationRegex.Match(line);
            if (match.Success) addedLombok.Add(match.Groups[1].Value);
        }

        // @Data silindi → getter/setter kalkti, serialization kirilabilir
        if (removedLombok.Contains("Data") && !addedLombok.Contains("Data"))
        {
            atoms.Add(new ChangeAtom(
                ChangeType.Other, file.FilePath, layer,
                "@Data silindi — getter/setter/toString/equals/hashCode etkilendi, JSON serialization kirilabilir!"));
        }

        // @Builder degisimi → nesne olusturma etkilenir
        if (removedLombok.Contains("Builder") && !addedLombok.Contains("Builder"))
        {
            atoms.Add(new ChangeAtom(
                ChangeType.Other, file.FilePath, layer,
                "@Builder silindi — builder pattern kullanan tum consumer'lar etkilenir"));
        }
    }

    public static FileLayer DetermineLayer(string filePath)
    {
        var normalized = filePath.Replace('\\', '/').ToLowerInvariant();

        // .NET patterns
        if (normalized.Contains("/models/") || normalized.Contains("/entities/") || normalized.Contains(".domain/models"))
            return FileLayer.Models;
        if (normalized.Contains("/controllers/"))
            return FileLayer.Controllers;
        if (normalized.Contains("/services/") || normalized.Contains(".domain/services"))
            return FileLayer.Services;
        if (normalized.Contains("/contracts/") || normalized.Contains("/interfaces/"))
            return FileLayer.Contracts;
        if (normalized.Contains("/httpclients/") || normalized.Contains("/httpclient"))
            return FileLayer.HttpClients;
        if (normalized.Contains("/infrastructure/"))
            return FileLayer.Infrastructure;
        if (normalized.Contains("/helpers/") || normalized.Contains("/extensions/") || normalized.Contains("/utils/"))
            return FileLayer.Helpers;
        if (normalized.Contains("/tests/") || normalized.Contains(".tests/") || normalized.Contains("/test/"))
            return FileLayer.Tests;

        // Go patterns
        if (normalized.EndsWith(".go"))
        {
            if (normalized.Contains("/handler/") || normalized.Contains("/route")) return FileLayer.Controllers;
            if (normalized.Contains("/model/") || normalized.Contains("/entity/")) return FileLayer.Models;
            if (normalized.Contains("/service/")) return FileLayer.Services;
            if (normalized.Contains("/client/") || normalized.Contains("/http")) return FileLayer.HttpClients;
            if (normalized.Contains("_test.go")) return FileLayer.Tests;
            if (normalized.Contains("/cmd/") || normalized.Contains("/internal/")) return FileLayer.Services;
        }

        // JS/TS/React patterns
        if (normalized.EndsWith(".ts") || normalized.EndsWith(".js") || normalized.EndsWith(".tsx") || normalized.EndsWith(".jsx"))
        {
            if (normalized.Contains("/route") || normalized.Contains("/controller")) return FileLayer.Controllers;
            if (normalized.Contains("/model") || normalized.Contains("/type") || normalized.Contains("/dto")) return FileLayer.Models;
            if (normalized.Contains("/service") || normalized.Contains("/usecase")) return FileLayer.Services;
            if (normalized.Contains("/client") || normalized.Contains("/api/")) return FileLayer.HttpClients;
            if (normalized.Contains("/component") || normalized.Contains("/page")) return FileLayer.Controllers;
            if (normalized.Contains(".test.") || normalized.Contains(".spec.") || normalized.Contains("__test")) return FileLayer.Tests;
            if (normalized.Contains("/hook") || normalized.Contains("/util") || normalized.Contains("/helper")) return FileLayer.Helpers;
            if (normalized.Contains("/store") || normalized.Contains("/redux") || normalized.Contains("/context")) return FileLayer.Services;
        }

        // Java/Spring patterns (Hexagonal Architecture)
        if (normalized.Contains("/controller/") || normalized.Contains("/rest/") && normalized.Contains("controller"))
            return FileLayer.Controllers;
        if (normalized.Contains("/model/") || normalized.Contains("/entity/") || normalized.Contains("/dto/"))
            return FileLayer.Models;
        if (normalized.Contains("/usecase/") || normalized.Contains("/service/") || normalized.Contains("/handler/"))
            return FileLayer.Services;
        if (normalized.Contains("/port/"))
            return FileLayer.Contracts;
        if (normalized.Contains("/adapter/out/") || normalized.Contains("/httpclient/") || normalized.Contains("httpadapt"))
            return FileLayer.HttpClients;
        if (normalized.Contains("/adapter/") || normalized.Contains("/infrastructure/"))
            return FileLayer.Infrastructure;
        if (normalized.Contains("/consumer/") || normalized.Contains("/scheduler/"))
            return FileLayer.Infrastructure;
        if (normalized.Contains("/domain/"))
            return FileLayer.Services; // Java domain layer ≈ .NET service layer
        if (normalized.Contains("/config/") || normalized.Contains("/configuration/"))
            return FileLayer.Infrastructure;

        return FileLayer.Unknown;
    }
}
