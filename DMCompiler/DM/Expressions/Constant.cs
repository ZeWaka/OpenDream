using OpenDreamShared.Compiler;
using OpenDreamShared.Dream;
using OpenDreamShared.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace DMCompiler.DM.Expressions {
    abstract class Constant : DMExpression {
        public Constant(Location location) : base(location) { }

        public sealed override bool TryAsConstant(out Constant constant) {
            constant = this;
            return true;
        }

        public abstract Constant CopyWithNewLocation(Location loc);

        public abstract bool IsTruthy();

        #region Unary Operations
        public Constant Not() {
            return new Number(Location, IsTruthy() ? 0 : 1);
        }

        public virtual Constant Negate() {
            throw new CompileErrorException(Location, $"const operation \"-{this}\" is invalid");
        }

        public virtual Constant BinaryNot() {
            throw new CompileErrorException(Location, $"const operation \"~{this}\" is invalid");
        }
        #endregion

        #region Binary Operations

        public virtual Constant Add(Constant rhs) {
            throw new CompileErrorException(Location, $"const operation \"{this} + {rhs}\" is invalid");
        }

        public virtual Constant Subtract(Constant rhs) {
            throw new CompileErrorException(Location, $"const operation \"{this} - {rhs}\" is invalid");
        }

        public virtual Constant Multiply(Constant rhs) {
            throw new CompileErrorException(Location, $"const operation \"{this} * {rhs}\" is invalid");
        }

        public virtual Constant Divide(Constant rhs) {
            throw new CompileErrorException(Location, $"const operation \"{this} / {rhs}\" is invalid");
        }

        public virtual Constant Modulo(Constant rhs) {
            throw new CompileErrorException(Location, $"const operation \"{this} % {rhs}\" is invalid");
        }

        public virtual Constant ModuloModulo(Constant rhs) {
            throw new CompileErrorException(Location, $"const operation \"{this} % {rhs}\" is invalid");
        }

        public virtual Constant Power(Constant rhs) {
            throw new CompileErrorException(Location, $"const operation \"{this} ** {rhs}\" is invalid");
        }

        public virtual Constant LeftShift(Constant rhs) {
            throw new CompileErrorException(Location, $"const operation \"{this} << {rhs}\" is invalid");
        }

        public virtual Constant RightShift(Constant rhs) {
            throw new CompileErrorException(Location, $"const operation \"{this} >> {rhs}\" is invalid");
        }

        public virtual Constant BinaryAnd(Constant rhs) {
            throw new CompileErrorException(Location, $"const operation \"{this} & {rhs}\" is invalid");
        }

        public virtual Constant BinaryXor(Constant rhs) {
            throw new CompileErrorException(Location, $"const operation \"{this} ^ {rhs}\" is invalid");
        }

        public virtual Constant BinaryOr(Constant rhs) {
            throw new CompileErrorException(Location, $"const operation \"{this} | {rhs}\" is invalid");
        }

        public virtual Constant GreaterThan(Constant rhs) {
            throw new CompileErrorException(Location, $"const operation \"{this} > {rhs}\" is invalid");
        }

        public virtual Constant GreaterThanOrEqual(Constant rhs) {
            throw new CompileErrorException(Location, $"const operation \"{this} >= {rhs}\" is invalid");
        }

        public virtual Constant LessThan(Constant rhs) {
            throw new CompileErrorException(Location, $"const operation \"{this} < {rhs}\" is invalid");
        }

        public virtual Constant LessThanOrEqual(Constant rhs) {
            throw new CompileErrorException(Location, $"const operation \"{this} <= {rhs}\" is invalid");
        }
        // Not a virtual because it'd be very annoying to write this for every Constant subtype
        public Constant Equal(Constant rhs) {
            switch (this) {
                case Expressions.Null nil:
                    if (rhs is Expressions.Null) {
                        return Number.True(Location);
                    }
                    break;
                case Expressions.Number lhsNumber:
                    if (rhs is Expressions.Number rhsNumber) {
                        return Number.BoolToNumber(Location, lhsNumber.Value == rhsNumber.Value);
                    }
                    break;
                case Expressions.String lhsString:
                    if (rhs is Expressions.String rhsString) {
                        return Number.BoolToNumber(Location, lhsString.Value == rhsString.Value);
                    }
                    break;
                case Expressions.Resource lhsResource:
                    if (rhs is Expressions.Resource rhsResource) {
                        return Number.BoolToNumber(Location, lhsResource.FilePath == rhsResource.FilePath);
                    }
                    break;
                case Expressions.Path lhsPath:
                    if (rhs is Expressions.Path rhsPath) {
                        return Number.BoolToNumber(Location, lhsPath.Value == rhsPath.Value);
                    }
                    break;
                default:
                    break;
            }
            return Number.False(Location);
        }

        public Constant NotEqual(Constant rhs) {
            return Number.BoolToNumber(Location, !this.Equal(rhs).IsTruthy());
        }
        #endregion
    }

    /// <summary>
    /// Stores a literal null.
    /// </summary>
    /// <remarks>
    /// The reason we inherit from Number here is that, in all arithmetic operations, Null behaves identically to a numeric value of 0.
    /// </remarks>
    sealed class Null : Number {
        public override float Value {
            get {
                EmitNullWarning();
                return base.Value;
            }
        }
        public Null(Location location) : base(location, 0f) { }

        public override void EmitPushValue(DMObject dmObject, DMProc proc) {
            proc.PushNull();
        }

        public override Constant CopyWithNewLocation(Location loc) {
            return new Null(loc);
        }

        public override bool IsTruthy() => false;

        public override bool TryAsJsonRepresentation(out object? json) {
            json = null;
            return true;
        }

/*
    Null has some extremely cursed behaviour with how it compares with strings. See below:
    ASSERT( (null > "") == 0 )
    ASSERT( (null >= "") == 1 )
    ASSERT( (null == "") == 0 )
    ASSERT( (null <= "") == 1 )
    ASSERT( (null < "") == 0 )
    ASSERT( (null != "") == 1 )
    Further, the opposite comparisons (like "" > null) just runtime. This is a quirky edgecase.
    The following is HOPEFULLY comparison/equality ops that abide to this ridiculous behaviour.
*/

        public override Constant GreaterThan(Constant rhs) {
            if(rhs is Number rhsNum) {
                return new Number(Location, (0 > rhsNum.Value) ? 1 : 0);
            }
            if(rhs is String rhsString) {
                //null > "" is 0
                //null > "foo" is 0
                return Number.False(Location);
            }
            return base.GreaterThan(rhs);
        }

        public override Constant GreaterThanOrEqual(Constant rhs) {
            if (rhs is Number rhsNum) {
                return new Number(Location, (0 >= rhsNum.Value) ? 1 : 0);
            }
            if(rhs is String rhsString) {
                if(rhsString.IsEmpty) { //(null >= "") == 1
                    DMCompiler.Emit(WarningCode.StupidNullOperation, Location, $"'null >= \"{rhsString.Value}\"' has the unexpected value of TRUE");
                    return Number.True(Location);
                }
                return Number.False(Location);
            }
            return base.GreaterThanOrEqual(rhs);

        }

        public override Constant LessThan(Constant rhs) {
            if (rhs is Number rhsNum) {
                return new Number(Location, (0 < rhsNum.Value) ? 1 : 0);
            }
            if(rhs is String rhsString) {
                //null < "" is 0
                //null < "foo" is 1
                if(rhsString.IsEmpty)
                    return Number.False(Location);
                else
                    return Number.True(Location);
            }
            return base.LessThan(rhs);

        }

        public override Constant LessThanOrEqual(Constant rhs) {
            if (rhs is Number rhsNum) {
                return new Number(Location, (0 <= rhsNum.Value) ? 1 : 0);
            }
            if(rhs is String rhsString) {
                if(rhsString.IsEmpty) { //null <= "" is 1!
                    DMCompiler.Emit(WarningCode.StupidNullOperation,Location, $"'null <= \"{rhsString.Value}\"' has the unexpected value of TRUE");
                }
                //null <= "foo" is 1, too
                return Number.True(Location);
            }
            return base.LessThanOrEqual(rhs);
        }
        void EmitNullWarning() {
            DMCompiler.Emit(WarningCode.StupidNullOperation, Location, "Null value is coerced to 0 by this operation");
        }

        public override Constant Add(Constant rhs) {
            if (rhs is String rhsString) {
                DMCompiler.Emit(WarningCode.StupidNullOperation, Location, "Null value is ignored in addition with string");
                return rhsString;
            }
            return base.Add(rhs);
        }
    }

    // 4.0, -4.0
    class Number : Constant {
        public virtual float Value { get; }

        public Number(Location location, int value) : base(location) {
            Value = value;
        }

        public Number(Location location, float value) : base(location) {
            Value = value;
        }

        /// <summary> Makes a TRUE. </summary>
        public static Number True(Location location) {
            return new Number(location, 1f);
        }

        /// <summary> Makes a FALSE. </summary>
        public static Number False(Location location) {
            return new Number(location, 0f);
        }

        public static Number BoolToNumber(Location location, bool value) {
            if(value) {
                return True(location);
            } else {
                return False(location);
            }
        }

        public override void EmitPushValue(DMObject dmObject, DMProc proc) {
            proc.PushFloat(Value);
        }

        public override Constant CopyWithNewLocation(Location loc) {
            return new Number(loc,Value);
        }


        public override bool IsTruthy() => Value != 0;

        public override bool TryAsJsonRepresentation(out object? json) {
            // Positive/Negative infinity cannot be represented in JSON and need a special value
            if (float.IsPositiveInfinity(Value)) {
                json = new Dictionary<string, JsonVariableType>() {
                    {"type", JsonVariableType.PositiveInfinity}
                };
            } else if (float.IsNegativeInfinity(Value)) {
                json = new Dictionary<string, JsonVariableType>() {
                    {"type", JsonVariableType.NegativeInfinity}
                };
            } else {
                json = Value;
            }

            return true;
        }

        public override Constant Negate() {
            return new Number(Location, -Value);
        }

        public override Constant BinaryNot() {
            return new Number(Location, ~(int)Value);
        }

        public override Constant Add(Constant rhs) {
            if (rhs is not Number rhsNum) {
                return base.Add(rhs);
            }

            return new Number(Location, Value + rhsNum.Value);
        }

        public override Constant Subtract(Constant rhs) {
            if (rhs is not Number rhsNum) {
                return base.Add(rhs);
            }

            return new Number(Location, Value - rhsNum.Value);
        }

        public override Constant Multiply(Constant rhs) {
            if (rhs is not Number rhsNum) {
                return base.Add(rhs);
            }

            return new Number(Location, Value * rhsNum.Value);
        }

        public override Constant Divide(Constant rhs) {
            if (rhs is not Number rhsNum) {
                return base.Add(rhs);
            }

            return new Number(Location, Value / rhsNum.Value);
        }

        public override Constant Modulo(Constant rhs) {
            if (rhs is not Number rhsNum) {
                return base.Add(rhs);
            }

            return new Number(Location, Value % rhsNum.Value);
        }

        public override Constant ModuloModulo(Constant rhs) {
            if (rhs is not Number rhsNum) {
                return base.ModuloModulo(rhs);
            }

            // BYOND docs say that A %% B is equivalent to B * fract(A/B)
            var fraction = Value / rhsNum.Value;
            fraction -= MathF.Truncate(fraction);
            return new Number(Location, fraction * rhsNum.Value);
        }

        public override Constant Power(Constant rhs) {
            if (rhs is not Number rhsNum) {
                return base.Add(rhs);
            }

            return new Number(Location, MathF.Pow(Value, rhsNum.Value));
        }

        public override Constant LeftShift(Constant rhs) {
            if (rhs is not Number rhsNum) {
                return base.Add(rhs);
            }

            return new Number(Location, ((int)Value) << ((int)rhsNum.Value));
        }

        public override Constant RightShift(Constant rhs) {
            if (rhs is not Number rhsNum) {
                return base.Add(rhs);
            }

            return new Number(Location, ((int)Value) >> ((int)rhsNum.Value));
        }


        public override Constant BinaryAnd(Constant rhs) {
            if (rhs is not Number rhsNum) {
                return base.Add(rhs);
            }

            return new Number(Location, ((int)Value) & ((int)rhsNum.Value));
        }


        public override Constant BinaryXor(Constant rhs) {
            if (rhs is not Number rhsNum) {
                return base.Add(rhs);
            }

            return new Number(Location, ((int)Value) ^ ((int)rhsNum.Value));
        }


        public override Constant BinaryOr(Constant rhs) {
            if (rhs is not Number rhsNum) {
                return base.Add(rhs);
            }

            return new Number(Location, ((int)Value) | ((int)rhsNum.Value));
        }

        public override Constant GreaterThan(Constant rhs) {
            if (rhs is not Number rhsNum) {
                return base.GreaterThan(rhs);
            }
            return new Number(Location, (Value > rhsNum.Value) ? 1 : 0);
        }

        public override Constant GreaterThanOrEqual(Constant rhs) {
            if (rhs is not Number rhsNum) {
                return base.GreaterThanOrEqual(rhs);
            }
            return new Number(Location, (Value >= rhsNum.Value) ? 1 : 0);
        }

        public override Constant LessThan(Constant rhs) {
            if (rhs is not Number rhsNum) {
                return base.LessThan(rhs);
            }
            return new Number(Location, (Value < rhsNum.Value) ? 1 : 0);
        }

        public override Constant LessThanOrEqual(Constant rhs) {
            if (rhs is not Number rhsNum) {
                return base.LessThanOrEqual(rhs);
            }
            return new Number(Location, (Value <= rhsNum.Value) ? 1 : 0);
        }
    }

    // "abc"
    sealed class String : Constant {
        public string Value { get; }

        public String(Location location, string value) : base(location) {
            Value = value;
        }

        public override void EmitPushValue(DMObject dmObject, DMProc proc) {
            proc.PushString(Value);
        }


        public override Constant CopyWithNewLocation(Location loc) {
            return new String(loc, Value);
        }

        public override bool IsTruthy() => Value.Length != 0;
        public bool IsEmpty => Value.Length == 0;

        public override bool TryAsJsonRepresentation(out object? json) {
            json = Value;
            return true;
        }

        public override Constant Add(Constant rhs) {
            if (rhs is not String rhsString) {
                if(rhs is Expressions.Null) {
                    DMCompiler.ForcedWarning(rhs.Location, "Null value is ignored in addition with string");
                    return this;
                }
                return base.Add(rhs);
            }

            return new String(Location, Value + rhsString.Value);
        }
    }

    // '[resource_path]'
    // Where resource_path is one of:
    //   - path relative to project root (.dme file location)
    //   - path relative to current .dm source file location
    //
    // Note: built .json file depends on resource files, so they should be moving with it
    // TODO: cache resources to a single .rsc file, as BYOND does
    internal sealed class Resource : Constant {
        private static readonly EnumerationOptions SearchOptions = new() {
            MatchCasing = MatchCasing.CaseInsensitive
        };

        public readonly string FilePath;
        private bool _isAmbiguous;

        public Resource(Location location, string filePath) : base(location) {
            string? finalFilePath = null;

            var outputDir = System.IO.Path.GetDirectoryName(DMCompiler.Settings.Files[0]) ?? "/";
            if (string.IsNullOrEmpty(outputDir))
                outputDir = "./";

            var fileName = System.IO.Path.GetFileName(filePath);
            var fileDir = System.IO.Path.GetDirectoryName(filePath) ?? string.Empty;
            var directory = FindDirectory(outputDir, fileDir);
            if (directory != null) {
                // Perform a case-insensitive search for the file
                finalFilePath = FindFile(directory, fileName);
            }

            // Search relative to the source file if it wasn't in the project's directory
            if (finalFilePath == null) {
                var sourceDir = System.IO.Path.Combine(outputDir, System.IO.Path.GetDirectoryName(Location.SourceFile) ?? string.Empty);
                directory = FindDirectory(sourceDir, fileDir);

                if (directory != null)
                    finalFilePath = FindFile(directory, fileName);
            }

            if (finalFilePath != null) {
                FilePath = System.IO.Path.GetRelativePath(outputDir, finalFilePath);

                if (_isAmbiguous) {
                    DMCompiler.Emit(WarningCode.AmbiguousResourcePath, Location,
                        $"Resource {filePath} has multiple case-insensitive matches, using {FilePath}");
                }
            } else {
                DMCompiler.Emit(WarningCode.ItemDoesntExist, Location, $"Cannot find file '{filePath}'");
                FilePath = filePath;
            }
        }

        public override void EmitPushValue(DMObject dmObject, DMProc proc) {
            proc.PushResource(FilePath);
        }

        public override Constant CopyWithNewLocation(Location loc) {
            return new Resource(loc, FilePath);
        }

        public override bool IsTruthy() => true;

        public override bool TryAsJsonRepresentation(out object? json) {
            json = new Dictionary<string, object>() {
                { "type", JsonVariableType.Resource },
                { "resourcePath", FilePath }
            };

            return true;
        }

        /// <summary>
        /// Performs a recursive case-insensitive for a directory.<br/>
        /// Marks the resource as ambiguous if multiple are found.
        /// </summary>
        /// <param name="directory">Directory to search in (case-sensitive)</param>
        /// <param name="searching">Directory to search for (case-insensitive)</param>
        /// <returns>The found directory, null if none</returns>
        private string? FindDirectory(string directory, string searching) {
            var searchingDirectories = searching.Split('/', StringSplitOptions.RemoveEmptyEntries);

            foreach (var searchingDirectory in searchingDirectories) {
                string[] directories = Directory.GetDirectories(directory, searchingDirectory, SearchOptions);

                if (directories.Length == 0)
                    return null;
                else if (directories.Length > 1)
                    _isAmbiguous = true;

                directory = directories[0];
            }

            return directory;
        }

        /// <summary>
        /// Performs a case-insensitive search for a file inside a directory.<br/>
        /// Marks the resource as ambiguous if multiple are found.
        /// </summary>
        /// <param name="directory">Directory to search in (case-sensitive)</param>
        /// <param name="searching">File to search for (case-insensitive)</param>
        /// <returns>The found file, null if none</returns>
        private string? FindFile(string directory, string searching) {
            var files = Directory.GetFiles(directory, searching, SearchOptions);

            // GetFiles() can't find "..ogg" on Linux for some reason, so try a direct check for the file
            if (files.Length == 0) {
                string combined = System.IO.Path.Combine(directory, searching);

                return File.Exists(combined) ? combined : null;
            } else if (files.Length > 1) {
                _isAmbiguous = true;
            }

            return System.IO.Path.Combine(directory, files[0]);
        }
    }

    // /a/b/c
    sealed class Path : Constant {
        public DreamPath Value { get; }

        /// <summary>
        /// The DMObject this expression resides in. Used for path searches.
        /// </summary>
        private readonly DMObject _dmObject;

        private enum PathType {
            TypeReference,
            ProcReference,
            ProcStub,
            VerbStub
        }

        public Path(Location location, DMObject dmObject, DreamPath value) : base(location) {
            Value = value;
            _dmObject = dmObject;
        }

        public override void EmitPushValue(DMObject dmObject, DMProc proc) {
            if (!TryResolvePath(out var pathInfo)) {
                proc.PushNull();
                return;
            }

            switch (pathInfo.Value.Type) {
                case PathType.TypeReference:
                    proc.PushType(pathInfo.Value.Id);
                    break;
                case PathType.ProcReference:
                    proc.PushProc(pathInfo.Value.Id);
                    break;
                case PathType.ProcStub:
                    proc.PushProcStub(pathInfo.Value.Id);
                    break;
                case PathType.VerbStub:
                    proc.PushVerbStub(pathInfo.Value.Id);
                    break;
                default:
                    DMCompiler.ForcedError(Location, $"Invalid PathType {pathInfo.Value.Type}");
                    break;
            }
        }

        public override string GetNameof(DMObject dmObject, DMProc proc) {
            return Value.LastElement;
        }

        /// <summary>
        /// This is used to make sure that Constants properly remember their context, <br/>
        /// even as they get folded about the place, into various locations in the source code.
        /// </summary>
        public override Constant CopyWithNewLocation(Location loc) {
            return new Path(loc, _dmObject, Value);
        }

        public override bool IsTruthy() => true;

        public override bool TryAsJsonRepresentation(out object? json) {
            if (!TryResolvePath(out var pathInfo)) {
                json = null;
                return false;
            }

            JsonVariableType jsonType = pathInfo.Value.Type switch {
                PathType.TypeReference => JsonVariableType.Type,
                PathType.ProcReference => JsonVariableType.Proc,
                PathType.ProcStub => JsonVariableType.ProcStub,
                PathType.VerbStub => JsonVariableType.VerbStub
            };

            json = new Dictionary<string, object>() {
                { "type", jsonType },
                { "value", pathInfo.Value.Id }
            };

            return true;
        }

        private bool TryResolvePath([NotNullWhen(true)] out (PathType Type, int Id)? pathInfo) {
            DreamPath path = Value;

            // An upward search with no left-hand side
            if (Value.Type == DreamPath.PathType.UpwardSearch) {
                DreamPath? foundPath = DMObjectTree.UpwardSearch(_dmObject.Path, path);
                if (foundPath == null) {
                    DMCompiler.Emit(WarningCode.ItemDoesntExist, Location, $"Could not find path {path}");

                    pathInfo = null;
                    return false;
                }

                path = foundPath.Value;
            }

            // /datum/proc and /datum/verb
            if (Value.LastElement is "proc" or "verb") {
                DreamPath typePath = Value.FromElements(0, -2);
                if (!DMObjectTree.TryGetTypeId(typePath, out var ownerId)) {
                    DMCompiler.Emit(WarningCode.ItemDoesntExist, Location, $"Type {typePath} does not exist");

                    pathInfo = null;
                    return false;
                }

                pathInfo = Value.LastElement switch {
                    "proc" => (PathType.ProcStub, ownerId),
                    "verb" => (PathType.VerbStub, ownerId),
                    _ => throw new InvalidOperationException($"Last element of {Value} is not \"proc\" or \"verb\"")
                };
                return true;
            }

            // /datum/proc/foo
            int procIndex = path.FindElement("proc");
            if (procIndex == -1) procIndex = path.FindElement("verb");
            if (procIndex != -1) {
                DreamPath withoutProcElement = path.RemoveElement(procIndex);
                DreamPath ownerPath = withoutProcElement.FromElements(0, -2);
                DMObject owner = DMObjectTree.GetDMObject(ownerPath, createIfNonexistent: false);
                string procName = path.LastElement;

                int? procId;
                if (owner == DMObjectTree.Root && DMObjectTree.TryGetGlobalProc(procName, out var globalProc)) {
                    procId = globalProc.Id;
                } else {
                    var procs = owner.GetProcs(procName);

                    procId = procs?[^1];
                }

                if (procId == null) {
                    DMCompiler.Emit(WarningCode.ItemDoesntExist, Location,
                        $"Type {ownerPath} does not have a proc named {procName}");

                    pathInfo = null;
                    return false;
                }

                pathInfo = (PathType.ProcReference, procId.Value);
                return true;
            }

            // Any other path
            if (DMObjectTree.TryGetTypeId(Value, out var typeId)) {
                pathInfo = (PathType.TypeReference, typeId);
                return true;
            } else {
                DMCompiler.Emit(WarningCode.ItemDoesntExist, Location, $"Type {Value} does not exist");

                pathInfo = null;
                return false;
            }
        }
    }
}
