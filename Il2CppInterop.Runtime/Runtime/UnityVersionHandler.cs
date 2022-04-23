using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Assembly;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Class;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.EventInfo;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Exception;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.FieldInfo;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Image;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.ParameterInfo;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.PropertyInfo;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Type;

namespace Il2CppInterop.Runtime.Runtime
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    internal class ApplicableToUnityVersionsSinceAttribute : Attribute
    {
        public string StartVersion { get; }

        public ApplicableToUnityVersionsSinceAttribute(string startVersion)
        {
            StartVersion = startVersion;
        }
    }

    public static class UnityVersionHandler
    {
        private static readonly Type[] InterfacesOfInterest;
        private static readonly Dictionary<Type, List<(Version Version, object Handler)>> VersionedHandlers = new();
        private static readonly Dictionary<Type, object> Handlers = new();

        private static Version UnityVersion = new(2018, 4, 20);
        // Version since which extra_arg is set to invoke_multicast, necessitating constructor calls
        private static readonly Version DelegatesGotComplexVersion = new Version(2021, 2, 0);

        internal static INativeAssemblyStructHandler assemblyStructHandler;
        internal static INativeClassStructHandler classStructHandler;
        internal static INativeEventInfoStructHandler eventInfoStructHandler;
        internal static INativeExceptionStructHandler exceptionStructHandler;
        internal static INativeFieldInfoStructHandler fieldInfoStructHandler;
        internal static INativeImageStructHandler imageStructHandler;
        internal static INativeMethodInfoStructHandler methodInfoStructHandler;
        internal static INativeParameterInfoStructHandler parameterInfoStructHandler;
        internal static INativePropertyInfoStructHandler propertyInfoStructHandler;
        internal static INativeTypeStructHandler typeStructHandler;

        static UnityVersionHandler()
        {
            var allTypes = GetAllTypesSafe();
            var interfacesOfInterest = allTypes.Where(t => t.IsInterface && typeof(INativeStructHandler).IsAssignableFrom(t) && t != typeof(INativeStructHandler)).ToArray();
            InterfacesOfInterest = interfacesOfInterest;

            foreach (var i in interfacesOfInterest) VersionedHandlers[i] = new();

            foreach (var handlerImpl in allTypes.Where(t => !t.IsAbstract && interfacesOfInterest.Any(i => i.IsAssignableFrom(t))))
                foreach (var startVersion in handlerImpl.GetCustomAttributes<ApplicableToUnityVersionsSinceAttribute>())
                {
                    var instance = Activator.CreateInstance(handlerImpl);
                    foreach (var i in handlerImpl.GetInterfaces())
                        if (interfacesOfInterest.Contains(i))
                            VersionedHandlers[i].Add((Version.Parse(startVersion.StartVersion), instance));
                }

            foreach (var handlerList in VersionedHandlers.Values)
                handlerList.Sort((a, b) => -a.Version.CompareTo(b.Version));

            RecalculateHandlers();
        }

        public static bool MustUseDelegateConstructor { get; private set; }

        private static void RecalculateHandlers()
        {
            Handlers.Clear();
            foreach (var type in InterfacesOfInterest)
            {
                foreach (var valueTuple in VersionedHandlers[type])
                {
                    if (valueTuple.Version > UnityVersion) continue;

                    Handlers[type] = valueTuple.Handler;
                    break;
                }
            }
            assemblyStructHandler = GetHandler<INativeAssemblyStructHandler>();
            classStructHandler = GetHandler<INativeClassStructHandler>();
            eventInfoStructHandler = GetHandler<INativeEventInfoStructHandler>();
            exceptionStructHandler = GetHandler<INativeExceptionStructHandler>();
            fieldInfoStructHandler = GetHandler<INativeFieldInfoStructHandler>();
            imageStructHandler = GetHandler<INativeImageStructHandler>();
            methodInfoStructHandler = GetHandler<INativeMethodInfoStructHandler>();
            parameterInfoStructHandler = GetHandler<INativeParameterInfoStructHandler>();
            propertyInfoStructHandler = GetHandler<INativePropertyInfoStructHandler>();
            typeStructHandler = GetHandler<INativeTypeStructHandler>();

            MustUseDelegateConstructor = UnityVersion >= DelegatesGotComplexVersion;
        }

        private static T GetHandler<T>()
        {
            if (Handlers.TryGetValue(typeof(T), out var result))
                return (T)result;

            Logger.Error($"No direct for {typeof(T).FullName} found for Unity {UnityVersion}; this likely indicates a severe error somewhere");

            throw new ApplicationException("No handler");
        }

        public static IntPtr CopyMethodInfoStruct(IntPtr origMethodInfo)
        {
            return GetHandler<INativeMethodInfoStructHandler>().CopyMethodInfoStruct(origMethodInfo);
        }

        private static Type[] GetAllTypesSafe()
        {
            try
            {
                return typeof(UnityVersionHandler).Assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException re)
            {
                return re.Types.Where(t => t != null).ToArray();
            }
        }

        /// <summary>
        ///     Initializes Unity interface for specified Unity version.
        /// </summary>
        /// <example>For Unity 2018.4.20, call <c>Initialize(2018, 4, 20)</c></example>
        public static void Initialize(int majorVersion, int minorVersion, int patchVersion)
        {
            UnityVersion = new Version(majorVersion, minorVersion, patchVersion);
            RecalculateHandlers();
        }



        //Assemblies
        public static INativeAssemblyStruct NewAssembly() =>
            assemblyStructHandler.CreateNewAssemblyStruct();

        public static unsafe INativeAssemblyStruct Wrap(Il2CppAssembly* assemblyPointer) =>
            assemblyStructHandler.Wrap(assemblyPointer);

        public static unsafe int AssemblySize() => assemblyStructHandler.Size();

        //Classes
        public static INativeClassStruct NewClass(int vTableSlots) =>
            classStructHandler.CreateNewClassStruct(vTableSlots);

        public static unsafe INativeClassStruct Wrap(Il2CppClass* classPointer) =>
            classStructHandler.Wrap(classPointer);

        public static unsafe int ClassSize() => classStructHandler.Size();

        //Events
        public static INativeEventInfoStruct NewEvent() =>
            eventInfoStructHandler.CreateNewEventInfoStruct();

        public static unsafe INativeEventInfoStruct Wrap(Il2CppEventInfo* eventInfoPointer) =>
            eventInfoStructHandler.Wrap(eventInfoPointer);

        public static unsafe int EventSize() => eventInfoStructHandler.Size();

        //Exceptions
        public static INativeExceptionStruct NewException() =>
            exceptionStructHandler.CreateNewExceptionStruct();

        public static unsafe INativeExceptionStruct Wrap(Il2CppException* exceptionPointer) =>
            exceptionStructHandler.Wrap(exceptionPointer);

        public static unsafe int ExceptionSize() => exceptionStructHandler.Size();

        //Fields
        public static INativeFieldInfoStruct NewField() =>
            fieldInfoStructHandler.CreateNewFieldInfoStruct();

        public static unsafe INativeFieldInfoStruct Wrap(Il2CppFieldInfo* fieldInfoPointer) =>
            fieldInfoStructHandler.Wrap(fieldInfoPointer);

        public static unsafe int FieldInfoSize() => fieldInfoStructHandler.Size();


        //Images
        public static INativeImageStruct NewImage() =>
            imageStructHandler.CreateNewImageStruct();

        public static unsafe INativeImageStruct Wrap(Il2CppImage* imagePointer) =>
            imageStructHandler.Wrap(imagePointer);

        public static unsafe int ImageSize() => imageStructHandler.Size();

        //Methods
        public static INativeMethodInfoStruct NewMethod() =>
            methodInfoStructHandler.CreateNewMethodStruct();

        public static unsafe INativeMethodInfoStruct Wrap(Il2CppMethodInfo* methodPointer) =>
            methodInfoStructHandler.Wrap(methodPointer);

        public static unsafe int MethodSize() => methodInfoStructHandler.Size();
        public static IntPtr GetMethodFromReflection(IntPtr method) =>
            methodInfoStructHandler.GetMethodFromReflection(method);


        //Parameters
        public static unsafe Il2CppParameterInfo*[] NewMethodParameterArray(int count) =>
            parameterInfoStructHandler.CreateNewParameterInfoArray(count);

        public static unsafe INativeParameterInfoStruct Wrap(Il2CppParameterInfo* parameterInfo) =>
            parameterInfoStructHandler.Wrap(parameterInfo);

        public static unsafe INativeParameterInfoStruct Wrap(Il2CppParameterInfo* parameterInfo, int index) =>
            parameterInfoStructHandler.Wrap(parameterInfo, index);

        public static bool ParameterInfoHasNamePosToken() =>
            parameterInfoStructHandler.HasNamePosToken;


        //Properties
        public static INativePropertyInfoStruct NewProperty() =>
            propertyInfoStructHandler.CreateNewPropertyInfoStruct();

        public static unsafe INativePropertyInfoStruct Wrap(Il2CppPropertyInfo* propertyInfoPointer) =>
            propertyInfoStructHandler.Wrap(propertyInfoPointer);

        public static unsafe int ParameterInfoSize() => parameterInfoStructHandler.Size();

        //Types
        public static INativeTypeStruct NewType() =>
            typeStructHandler.CreateNewTypeStruct();

        public static unsafe INativeTypeStruct Wrap(Il2CppTypeStruct* typePointer) =>
            typeStructHandler.Wrap(typePointer);
        public static unsafe int TypeSize() => typeStructHandler.Size();
    }
}