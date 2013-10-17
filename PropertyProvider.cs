using System;
using System.Collections.Generic;
using System.Text;

using System.Reflection;
using System.Reflection.Emit;
using System.Collections;
using System.Threading;

namespace Leleko.CSharp.ComponentModel
{
    /// <summary>
    /// Getter/Setter для свойства и описание свойства
    /// </summary>
    /// <remarks>Multiton с ключем PropertyInfo</remarks>
    public sealed class PropertyAccessor
    {
        /// <summary>
        /// Доступ ко внутренностям
        /// </summary>
        /// <remarks>Некорректное использование данного доступа, может привести к непредсказуемым последствиям? Уверены?</remarks>
        public static class Internal
        {
            public static PropertyAccessor Greate(PropertyInfo propertyInfo, Getter<object, object> getter, Setter<object, object> setter, Delegate originalGetter, Delegate originalSetter)
            {
                return new PropertyAccessor(propertyInfo, getter, setter, originalGetter, originalSetter);
            }
        }

        #region [ Constants ]

        /// <summary>
        /// Константа
        /// </summary>
        static readonly Type[] ArrTypeObj = new Type[] { typeof(object) };

        /// <summary>
        /// Константа
        /// </summary>
        static readonly Type[] ArrTypeObjObj = new Type[] { typeof(object), typeof(object) };

        #endregion

        /// <summary>
        /// Таблица аккессоров [PropertyInfo => PropertyAccessor]
        /// </summary>
        static readonly Hashtable AccessorsTable = new Hashtable(64);

        /// <summary>
        /// Получить access'ор св-ва
        /// </summary>
        /// <param name="propertyInfo">метаданные св-ва</param>
        /// <returns>access'ор</returns>
        /// <exception cref="System.ArgumentNullException">propertyInfo == null</exception>
        /// <exception cref="System.ArgumentException">привязка не сможет быть выполнена для св-ва индексатора</exception>
        /// <exception cref="System.InvalidProgramException">привязка вызовет ошибку если будет вызвана для метода неопределенного Generic-типа</exception>
        public static PropertyAccessor Get(PropertyInfo propertyInfo)
        {
            if (propertyInfo == null)
                throw new ArgumentNullException("propertyInfo");
            return (AccessorsTable[propertyInfo] as PropertyAccessor) ?? (new PropertyAccessor(propertyInfo));
        }

        #region [ Fields readonly ]

        readonly PropertyInfo propertyInfo;

        readonly Getter<object, object> getter;

        readonly Setter<object, object> setter;

        readonly Delegate originalGetter;
        
        readonly Delegate originalSetter;

        #endregion

        #region [ Properties ]

        /// <summary>
        /// св-во
        /// </summary>
        public PropertyInfo PoprertyInfo
        {
            get { return this.propertyInfo; }
        }

        /// <summary>
        /// get'ер
        /// </summary>
        public Getter<object, object> Getter
        {
            get { return this.getter; }
        }

        /// <summary>
        /// set'ер
        /// </summary>
        public Setter<object, object> Setter
        {
            get { return this.setter; }
        }

        /// <summary>
        /// оригинальный get'ер (типизированный)
        /// </summary>
        public Delegate OriginalGetter
        {
            get { return this.originalGetter; }
        }

        /// <summary>
        /// оригинальный set'ер (типизированный)
        /// </summary>
        public Delegate OriginalSetter
        {
            get { return this.originalSetter; }
        }

        #endregion

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="propertyInfo">метаданные св-ва</param>
        private PropertyAccessor(PropertyInfo propertyInfo)
        {
            this.propertyInfo = propertyInfo;

            Type typeObject = propertyInfo.DeclaringType;
            Type typeValue = propertyInfo.PropertyType;
            Type[] genericTypes = new Type[] { typeObject, typeValue };

            MethodInfo m = null;

            if ((m = propertyInfo.GetGetMethod(true)) != null) // извлекаем getter
            {
                // формируем оригинальный, для этого просто создаем делегат на метод
                this.originalGetter = Delegate.CreateDelegate(typeof(Getter<,>).MakeGenericType(genericTypes), m, true);
                
                // формируем универсальный, для этого оборачиваем метод в универсальную оболочку
                DynamicMethod dynamicMethod = new DynamicMethod(string.Concat(typeObject.FullName, ".", m.Name), typeof(object), ArrTypeObj, true);
                var il = dynamicMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(typeObject.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, typeObject); // если тип не ссылочный - то извлекаем значение из ссылки
                il.Emit(OpCodes.Callvirt, m);
                if (typeValue.IsValueType)
                    il.Emit(OpCodes.Box, typeValue);    // если тип не ссылочный - то оборачиваем его в объект
                il.Emit(OpCodes.Ret);
                // создаем динамический метод
                this.getter = dynamicMethod.CreateDelegate(typeof(Getter<object, object>)) as Getter<object, object>;
            }

            if ((m = propertyInfo.GetSetMethod(true)) != null) // извлекаем setter
            {
                // формируем оригинальный, для этого просто создаем делегат на метод
                this.originalSetter = Delegate.CreateDelegate(typeof(Setter<,>).MakeGenericType(genericTypes), m, true);
                
                // формируем оригинальный, для этого просто создаем делегат на метод
                DynamicMethod dynamicMethod = new DynamicMethod(string.Concat(typeObject.FullName, ".", m.Name), null, ArrTypeObjObj, true);
                var il = dynamicMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(typeObject.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, typeObject);
                il.Emit(OpCodes.Ldarg_1);
                if (typeValue != typeof(object))
                    il.Emit(typeValue.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, typeValue);
                il.Emit(OpCodes.Callvirt, m);
                il.Emit(OpCodes.Ret);
                // создаем динамический метод
                this.setter = dynamicMethod.CreateDelegate(typeof(Setter<object, object>)) as Setter<object, object>;
            }

            // Регистрируем accessor в таблице
            Hashtable accessorsTable = AccessorsTable;
            lock (accessorsTable.SyncRoot)
                accessorsTable[propertyInfo] = this;
        }

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="propertyInfo">метаданные св-ва</param>
        /// <param name="getter">getter</param>
        /// <param name="setter">setter</param>
        /// <param name="originalGetter">оригинальный(типизированный) getter</param>
        /// <param name="originalSetter">оригинальный(типизированный) setter</param>
        /// <remarks>Опасен(!!!)</remarks>
        private PropertyAccessor(PropertyInfo propertyInfo, Getter<object, object> getter, Setter<object, object> setter, Delegate originalGetter, Delegate originalSetter)
        {
            this.propertyInfo = propertyInfo;
            this.getter = getter;
            this.setter = setter;
            this.originalGetter = originalGetter;
            this.originalSetter = originalSetter;
        }
    }

    /// <summary>
    /// Проводник access'оров св-в
    /// </summary>
    public static class PropertyProvider
    {
        #region [ Constants ]

        /// <summary>
        /// Пустая таблица
        /// </summary>
        static readonly Hashtable EmptyTable = new Hashtable(0);

        #endregion

        /// <summary>
        /// Таблица св-в [Type => [string => PropertyAccessor]]
        /// </summary>
        /// <remarks>при инициализации добавляем пустой список св-в базовому типу(object)</remarks>
        static readonly Hashtable TypesTable = new Hashtable(16) { { typeof(object), EmptyTable} };

        /// <summary>
        /// Таблица публичных св-в [Type => [string => PropertyAccessor]]
        /// </summary>
        /// <remarks>при инициализации добавляем пустой список св-в базовому типу(object)</remarks>
        static readonly Hashtable TypesPublicTable = new Hashtable(16) { { typeof(object), EmptyTable } };

        /// <summary>
        /// Получить список св-в
        /// </summary>
        /// <param name="type">тип</param>
        /// <param name="isPublic">true: только публичные св-ва</param>
        /// <returns>коллекция PropertyAccessor</returns>
        public static ICollection GetAccessors(Type type, bool isPublic)
        {
            return 
                (isPublic)
                ? GetAccessors(TypesPublicTable, false, type, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public).Values
                : GetAccessors(TypesTable, false, type, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Values
                ;
        }

        /// <summary>
        /// Получить access'ор св-ва
        /// </summary>
        /// <param name="type">тип</param>
        /// <param name="propertyName">имя св-ва</param>
        /// <returns>access'ор</returns>
        public static PropertyAccessor GetAccessor(Type type, string propertyName)
        {
            return GetAccessors(TypesTable, false, type, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)[propertyName] as PropertyAccessor;
        }

        /// <summary>
        /// Доступ ко внутренностям
        /// </summary>
        /// <remarks>Некорректное использование данного доступа, может привести к непредсказуемым последствиям. Уверены?</remarks>
        public static class Internal
        {
            /// <summary>
            /// Получить таблицу св-в
            /// </summary>
            /// <param name="type">тип</param>
            /// <param name="isPublic">true: только публичные св-ва</param>
            /// <returns>таблица [string => PropertyAccessor]</returns>
            public static Hashtable GetAccessors(Type type, bool isPublic)
            {
                return
                    (isPublic)
                    ? PropertyProvider.GetAccessors(TypesPublicTable, false, type, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                    : PropertyProvider.GetAccessors(TypesTable, false, type, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ;
            }
        }

        #region [ Internal ]

        static Hashtable GetAccessors(Hashtable typesTable, bool isLock, Type type, BindingFlags bindingFlags)
        {
            // Table [String(propertyName) => PropertyAccessor]
            Hashtable accessorsTable = typesTable[type] as Hashtable; // Пытаемся получить коллекцию свойств для типа из кэша
            if (accessorsTable == null) // Не получили ?
            {
                if (isLock)
                    typesTable.Add(type, accessorsTable = CreateAccessors(typesTable, true, type, bindingFlags));   // Добавляем коллекцию св-в для типа в кэш
                else 
                    lock (typesTable.SyncRoot) // блокируем
                        typesTable.Add(type, accessorsTable = CreateAccessors(typesTable, true, type, bindingFlags));   // Добавляем коллекцию св-в для типа в кэш
            }
                    
            return accessorsTable;     // Возвращаем коллекцию свойств для типа
        }

        static Hashtable CreateAccessors(Hashtable typesTable, bool isLock, Type type, BindingFlags bindingFlags)
        {
            if (type.ContainsGenericParameters)
                return EmptyTable;   // для дженериков невозможно найти точки входа в св-ва (они не развернуты)
            Hashtable accessorsTable = 
                type.IsInterface
                ? CreateInterfaceAccessors(typesTable, isLock, type, bindingFlags)       // получить таблицу для интерфейса
                : CreateClassAccessors(typesTable, isLock, type, bindingFlags)           // получить таблицу для не интерфейса
                ;
            return (accessorsTable.Count != 0) ? accessorsTable : EmptyTable;
        }

        static Hashtable CreateInterfaceAccessors(Hashtable typesTable, bool isLock, Type type, BindingFlags bindingFlags)
        {
            // Table [String(propertyName) => PropertyAccessor]
            Hashtable accessorsTable = new Hashtable();
            foreach (var interfaceType in type.GetInterfaces())                 // пробегаем по всем базовым интерфейсам
                foreach (DictionaryEntry entry in GetAccessors(typesTable, isLock, interfaceType, bindingFlags))  // получаем список св-в для интерфейса
                    if (!accessorsTable.ContainsKey(entry.Key))
                        accessorsTable.Add(entry.Key, entry.Value);
            foreach (var propertyInfo in type.GetProperties(bindingFlags))          // пробегаем по всем св-вам текущего интерфейса
                if (propertyInfo.GetIndexParameters().Length == 0)                  // отсеиваем индексаторы
                    accessorsTable[propertyInfo.Name] = PropertyAccessor.Get(propertyInfo);  // запрашиваем аккессоры для свойства и добавляем(переопределяем) в таблицу
            return accessorsTable;
        }

        static Hashtable CreateClassAccessors(Hashtable typesTable, bool isLock, Type type, BindingFlags bindingFlags)
        {
            // Table [String(propertyName) => PropertyAccessor]
            Hashtable accessorsTable = new Hashtable(GetAccessors(typesTable, isLock, type.BaseType, bindingFlags));  // "наследуем" св-ва
            foreach (var propertyInfo in type.GetProperties(bindingFlags))
                if (propertyInfo.GetIndexParameters().Length == 0)                              // отсеивание св-в индексаторов
                    accessorsTable[propertyInfo.Name] = PropertyAccessor.Get(propertyInfo);     // запрашиваем аккессор св-ва и добавляем(переопределяем) в таблицу
            return accessorsTable;
        }

        #endregion
    }

    /// <summary>
    /// Проводник access'оров св-в типа [T]
    /// </summary>
    /// <typeparam name="T">тип</typeparam>
    public static class PropertyProvider<T>
    {
        /// <summary>
        /// Коллекция св-в выбранного класса ICollection of PropertyAccessor
        /// </summary>
        public static readonly ICollection AllAccessors = PropertyProvider.GetAccessors(typeof(T), false);

        /// <summary>
        /// Коллекция публичных св-в выбранного класса ICollection of PropertyAccessor
        /// </summary>
        public static readonly ICollection PublicAccessors = PropertyProvider.GetAccessors(typeof(T), true);

        /// <summary>
        /// Доступ ко внутренностям
        /// </summary>
        /// <remarks>Некорректное использование данного доступа, может привести к непредсказуемым последствиям. Уверены?</remarks>
        public static class Internal
        {
            /// <summary>
            /// Таблица св-в выбранного класса [string => PropertyAccessor]
            /// </summary>
            public static readonly Hashtable AllAccessors = PropertyProvider.Internal.GetAccessors(typeof(T), false);

            /// <summary>
            /// Таблица публичных св-в выбранного класса [string => PropertyAccessor]
            /// </summary>
            public static readonly Hashtable PublicAccessors = PropertyProvider.Internal.GetAccessors(typeof(T), true);
        }
    }
}
