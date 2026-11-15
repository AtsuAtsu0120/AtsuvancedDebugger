using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Atsuvanced.Debugger.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Atsuvanced.Debugger.Tests
{
    /// <summary>
    ///     The built-in features and pages are the "default implementation" on top of the core, like URP on top of SRP: they
    ///     must
    ///     be buildable by a user, so they may only use the core's public / protected API. Internals of the UI layer itself
    ///     (namespace <c>Atsuvanced.Debugger.UI</c>) are fine, since that layer would move as a whole.
    /// </summary>
    public class PublicApiParityTests
    {
        private const string UiNamespace = "Atsuvanced.Debugger.UI";

        private static readonly Type[] BuiltInPages =
        {
            typeof(ConsolePage), typeof(SystemInfoPage), typeof(OptionSectionPage), typeof(OptionSearchPage),
            typeof(PinnedOptionsPage), typeof(ProfilerPage), typeof(BugReportPage)
        };

        // ---- Minimal IL reader ------------------------------------------------------------

        private static readonly Dictionary<short, OpCode> OpCodeTable = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (OpCode)f.GetValue(null))
            .GroupBy(o => o.Value)
            .ToDictionary(g => g.Key, g => g.First());

        [Test]
        public void BuiltInPages_ArePublicAndInheritable()
        {
            foreach (var t in BuiltInPages)
            {
                Assert.IsTrue(t.IsPublic, t.Name);
                Assert.IsFalse(t.IsSealed, t.Name);
            }
        }

        /// <remarks>
        ///     Walks the IL of the pages and of every UI-layer type they reach. Constants are inlined by the compiler and
        ///     cannot be seen here, so internal <c>const</c>s slip through.
        /// </remarks>
        [Test]
        public void BuiltInFeatures_ArePublic_AndUseOnlyPublicCoreApi()
        {
            var features = BuiltInFeatures.Create().Select(f => f.GetType()).ToArray();
            foreach (var t in features)
            {
                Assert.IsTrue(t.IsPublic, t.Name);
            }

            var core = typeof(DebugPage).Assembly;
            var violations = new SortedSet<string>();
            var visited = new HashSet<Type>();
            var queue = new Queue<Type>(BuiltInPages.Concat(features));

            while (queue.Count > 0)
            {
                var type = queue.Dequeue();
                if (!visited.Add(type))
                {
                    continue;
                }

                foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                {
                    queue.Enqueue(nested);
                }

                foreach (var method in DeclaredMethods(type))
                foreach (var member in ReferencedMembers(method))
                {
                    var owner = member as Type ?? member.DeclaringType;
                    if (owner == null || owner.Assembly != core)
                    {
                        continue;
                    }

                    if (IsUiLayer(owner))
                    {
                        queue.Enqueue(Definition(owner));
                        continue;
                    }

                    if (!IsAccessibleFromOutside(member))
                    {
                        violations.Add($"{Name(type)} uses {Name(owner)}.{(member is Type ? "(type)" : member.Name)}");
                    }
                }
            }

            Assert.IsEmpty(violations,
                "Built-in features / pages use internal core API:\n" + string.Join("\n", violations));
            Assert.Greater(visited.Count, BuiltInPages.Length + features.Length,
                "Reached the UI types they use (views, overlays).");
        }

        private static bool IsUiLayer(Type t)
        {
            while (t.DeclaringType != null)
            {
                t = t.DeclaringType;
            }

            return t.Namespace == UiNamespace;
        }

        private static Type Definition(Type t)
        {
            return t.IsGenericType ? t.GetGenericTypeDefinition() : t;
        }

        private static bool IsAccessibleFromOutside(MemberInfo member)
        {
            switch (member)
            {
                case Type t:
                    return IsVisible(t);
                case MethodBase m:
                    return IsVisible(m.DeclaringType) && (m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly);
                case FieldInfo f:
                    return IsVisible(f.DeclaringType) && (f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly);
                default:
                    return true;
            }
        }

        private static bool IsVisible(Type t)
        {
            if (t.IsArray || t.IsByRef || t.IsPointer)
            {
                return IsVisible(t.GetElementType());
            }

            if (t.IsGenericParameter)
            {
                return true;
            }

            if (t.IsGenericType && !t.IsGenericTypeDefinition)
            {
                return IsVisible(t.GetGenericTypeDefinition()) && t.GetGenericArguments().All(IsVisible);
            }

            // Nested protected types are usable by subclasses.
            if (t.IsNested)
            {
                return (t.IsNestedPublic || t.IsNestedFamily || t.IsNestedFamORAssem) && IsVisible(t.DeclaringType);
            }

            return t.IsPublic;
        }

        private static string Name(Type t)
        {
            return t.DeclaringType != null ? Name(t.DeclaringType) + "+" + t.Name : t.Name;
        }

        private static IEnumerable<MethodBase> DeclaredMethods(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                       BindingFlags.Static | BindingFlags.DeclaredOnly;
            return type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags));
        }

        private static IEnumerable<MemberInfo> ReferencedMembers(MethodBase method)
        {
            var il = method.GetMethodBody()?.GetILAsByteArray();
            if (il == null)
            {
                yield break;
            }

            var typeArgs = method.DeclaringType != null && method.DeclaringType.IsGenericType
                ? method.DeclaringType.GetGenericArguments()
                : null;
            var methodArgs = method.IsGenericMethod ? method.GetGenericArguments() : null;

            var i = 0;
            while (i < il.Length)
            {
                short value = il[i++];
                if (value == 0xFE)
                {
                    value = unchecked((short)(0xFE00 | il[i++]));
                }

                if (!OpCodeTable.TryGetValue(value, out var op))
                {
                    yield break; // unknown opcode: stop rather than misread
                }

                switch (op.OperandType)
                {
                    case OperandType.InlineNone:
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar:
                        i += 1;
                        break;
                    case OperandType.InlineVar:
                        i += 2;
                        break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                        i += 8;
                        break;
                    case OperandType.InlineSwitch:
                        i += 4 + 4 * BitConverter.ToInt32(il, i);
                        break;
                    case OperandType.InlineMethod:
                    case OperandType.InlineField:
                    case OperandType.InlineType:
                    case OperandType.InlineTok:
                    {
                        var token = BitConverter.ToInt32(il, i);
                        i += 4;
                        MemberInfo member = null;
                        try
                        {
                            member = method.Module.ResolveMember(token, typeArgs, methodArgs);
                        }
                        catch (ArgumentException)
                        {
                        }

                        if (member != null)
                        {
                            yield return member;
                        }

                        break;
                    }
                    default: // InlineI, InlineBrTarget, InlineString, InlineSig, ShortInlineR
                        i += 4;
                        break;
                }
            }
        }

        // ---- New public API ---------------------------------------------------------------

        [Test]
        public void State_IsAvailableToPages_AndSharedWithTheBuiltInKeys()
        {
            var storage = new MemoryDebuggerStorage();
            var s = TestDebugger.Create(storage);
            s.State.Set("mygame/last-stage", "3");
            s.State.Save();
            Assert.AreEqual("3", storage.GetString(DebuggerState.KeyPrefix + "mygame/last-stage"));
            Assert.AreEqual(1, s.State.Clear());
            s.Dispose();
        }

        [Test]
        public void BugReportRequest_IsReadableAndCompletable_ThroughThePublicApi()
        {
            var s = TestDebugger.Create(new MemoryDebuggerStorage());
            bool? result = null;
            s.ShowBugReportSheet(ok => result = ok, false, "crash on load");

            var request = s.BugReports.Pending;
            Assert.IsNotNull(request);
            Assert.AreEqual("crash on load", request.Description);
            Assert.IsNull(request.Screenshot);

            s.BugReports.Complete(true);
            Assert.AreEqual(true, result);
            Assert.IsNull(s.BugReports.Pending);
            s.BugReports.Complete(false); // nothing pending: ignored
            Assert.AreEqual(true, result);
            s.Dispose();
        }

        [Test]
        public void OptionListView_CanBeUsedByCustomPages()
        {
            var s = TestDebugger.Create();
            s.AddOption(OptionDefinition.FromMethod("Kill", () => { }, "Enemies/Boss/Phase 2"));
            var list = new OptionListView(s, "Enemies");
            Assert.AreEqual("Enemies", list.CategoryPath);
            Assert.IsFalse(list.DirectOptionsOnly);

            list.Attach();
            list.OpenCategoryPath = "Enemies/Boss/Phase 2";
            list.Tick(0f);
            Assert.AreEqual("Enemies/Boss/Phase 2", list.OpenCategoryPath);
            Assert.IsTrue(list.Model.Rows.Any(r => r.Kind == OptionListModel.RowKind.Entry),
                "The open branch lists its options.");

            list.Dispose();
            s.Dispose();
        }

        [Test]
        public void ServiceTick_DrivesTheCurrentPage_WithoutAView()
        {
            var s = TestDebugger.Create(new MemoryDebuggerStorage());
            var page = new CountingPage();
            var tab = new DebugTabDefinition("counting", "Counting", () => page);
            s.Tabs.Add(tab);
            s.ShowDebugPanel("counting");

            s.Tick(0.016f);
            s.Tick(0.016f);
            Assert.AreEqual(2, page.Updates);

            using (s.Profiler.BeginRecording())
            {
                s.Tick(0.016f);
                Assert.AreEqual(1, s.Profiler.FrameTimes.Count, "Profiler sampling runs from the service tick.");
            }

            s.Dispose();
        }

        private sealed class CountingPage : DebugPage
        {
            public int Updates;

            protected override VisualElement CreateContent()
            {
                return new VisualElement();
            }

            protected override void OnUpdate()
            {
                Updates++;
            }
        }
    }
}