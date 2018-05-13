using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Data;
using System.ComponentModel;
namespace DataReaderDeserializer
{
    public static class GetDeserializer<T>
    {
        /// <summary>
        /// 使用反射
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static T ReflectDeserialize(IDataReader reader)
        {
            DbFieldReader dbReader = new DbFieldReader(reader);
            var columnList = Enumerable.Range(0, reader.FieldCount).Select(ordinal =>
            {
                Type fieldType = reader.GetFieldType(ordinal);
                return new
                {
                    Name = reader.GetName(ordinal),
                    Index = ordinal,
                    Type = fieldType,
                };
            });

            T model = Activator.CreateInstance<T>();
             typeof(T).GetProperties().ToList().ForEach(prop =>
            {
                var column = columnList.FirstOrDefault(obj => obj.Type == prop.PropertyType
                && string.Equals(prop.Name, obj.Name, StringComparison.CurrentCultureIgnoreCase));

                if (column == null)
                {
                    prop.SetValue(model, TypeHelper.GetDefault(prop.PropertyType), null);
                }else
                {
                    prop.SetValue(model, FieldReader.GetReaderMethod(prop.PropertyType).Invoke(dbReader, new object[] { column.Index }), null);
                }
            });
            return model;
        }
        /// <summary>
        /// 使用表达式
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static Func<IDataReader,T> Deserialize(IDataReader reader)
        {

            ParameterExpression readerParam = Expression.Parameter(typeof(FieldReader), "reader");
            ParameterExpression outerReaderParam = Expression.Parameter(typeof(IDataReader), "outerReader");
            var columnList = Enumerable.Range(0, reader.FieldCount).Select(ordinal =>
             {
                 Type fieldType = reader.GetFieldType(ordinal);
                 return new ColumnInfo
                 {
                     Type = fieldType,
                     Name = reader.GetName(ordinal),
                     Index = ordinal,
                     Reader = Expression.Call(readerParam, FieldReader.GetReaderMethod(fieldType), new Expression[] { Expression.Constant(ordinal) })
                 };
             });

            var propList = typeof(T).GetProperties().Select(prop =>
             {
                 var column = columnList.FirstOrDefault(obj => obj.Type == prop.PropertyType
                 && string.Equals(prop.Name, obj.Name, StringComparison.CurrentCultureIgnoreCase));

                 if (column == null)
                 {
                     return new TPropertyInfo
                     {
                         Info = prop,
                         Expression = Expression.Call(readerParam, typeof(FieldReader).GetMethod("GetDefault"), new[] { Expression.Constant(prop.PropertyType) })
                     };
                 }
                 return new TPropertyInfo
                 {
                     Info = prop,
                     Expression = column.Reader
                 };
             });

            ConstructorInfo[] constructorList = typeof(T).GetConstructors();

            Expression lambdaBody;
            if (constructorList.Any(ctor => ctor.GetParameters().Count() == 0))
            {
                lambdaBody = Expression.MemberInit(Expression.New(typeof(T)),
                     propList.Select(obj => Expression.Bind(obj.Info as MemberInfo, obj.Expression) as MemberBinding));
            }else
            {
                ConstructorInfo constructor = constructorList.FirstOrDefault(obj => obj.GetParameters().Count() == propList.Count());
                if (constructor == null) throw new ArgumentException(" 该类型构造函数参数个数与属性不匹配");
                lambdaBody = Expression.New(constructor, propList.Select(obj => obj.Expression));
            }
            Expression<Func<IDataReader, FieldReader>> CreateDbFieldReader = (_reader) => new DbFieldReader(_reader);
            LambdaExpression innerLambda = Expression.Lambda<Func<FieldReader, T>>(lambdaBody, readerParam);
            Expression fieldReader = Expression.Invoke(CreateDbFieldReader, outerReaderParam);

            return Expression.Lambda<Func<IDataReader, T>>(Expression.Invoke(innerLambda, fieldReader), outerReaderParam).Compile();
        }

        public static Func<IDataReader,T> ILDeserialize(IDataReader reader)
        {
            var columnList = Enumerable.Range(0, reader.FieldCount).Select(ordinal =>
            {
                Type fieldType = reader.GetFieldType(ordinal);
                return new
                {
                    Type = fieldType,
                    Name = reader.GetName(ordinal),
                    Index = ordinal,
                    Reader = FieldReader.GetReaderMethod(fieldType)
                };
            });

            Type targetType = typeof(T);
            var dm = new DynamicMethod(string.Format("Deserialize_{0}", Guid.NewGuid()), targetType, new[] { typeof(IDataReader) }, true);
            var il = dm.GetILGenerator();
            il.DeclareLocal(targetType);
            ConstructorInfo specializedConstructor = null;

            if (targetType.IsValueType)
            {
                il.Emit(OpCodes.Ldloca_S, (byte)0);
                il.Emit(OpCodes.Initobj, targetType);
            }else
            {
                //构造函数集合
               var constructors = targetType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(c => c.IsPublic ? 0 : (c.IsPrivate ? 2 : 1)).ThenBy(c => c.GetParameters().Length);

                ConstructorInfo defaultContructor = constructors.FirstOrDefault(c => c.GetParameters().Count() == 0);
                if (defaultContructor != null)
                {
                    il.Emit(OpCodes.Newobj, defaultContructor);
                    il.Emit(OpCodes.Stloc_0);
                    if (typeof(ISupportInitialize).IsAssignableFrom(targetType))
                    {
                        il.Emit(OpCodes.Ldloc_0);
                        il.EmitCall(OpCodes.Callvirt, typeof(ISupportInitialize).GetMethod("BeginInit"), null);
                    }
                }else
                {
                    specializedConstructor = constructors.First();
                }

                if (targetType.IsValueType)
                {
                    il.Emit(OpCodes.Ldloca_S, (byte)0);
                }else if(specializedConstructor==null)
                {
                    il.Emit(OpCodes.Ldloc_0);
                }
                typeof(T).GetProperties().Select(prop =>
                {
                    return new
                    {
                        Info = prop,
                        Setter = GetPropertySetter(prop, prop.PropertyType)
                    };
                }).ToList()
                .ForEach(tmp=>
                {
                    int valueIndex = il.DeclareLocal(tmp.Info.PropertyType).LocalIndex;
                    var column = columnList.FirstOrDefault(obj => obj.Type == tmp.Info.PropertyType
                   && string.Equals(tmp.Info.Name, obj.Name, StringComparison.CurrentCultureIgnoreCase));

                    if (column == null||tmp.Setter==null)
                    {
                        if (tmp.Info.PropertyType.IsValueType)
                        {
                            LoadLocalAddress(il, valueIndex);
                            il.Emit(OpCodes.Initobj, tmp.Info.PropertyType);
                            StoreLocal(il, valueIndex);
                            LoadLocal(il, valueIndex);
                        }
                        else
                        {
                            il.Emit(OpCodes.Ldnull);
                        }
                    }
                    else
                    {
                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Callvirt, column.Reader);
                        //StoreLocal(il, valueIndex);
                        il.Emit(OpCodes.Callvirt, tmp.Setter);
                    }
                });

                if (specializedConstructor != null)
                {
                    il.Emit(OpCodes.Newobj, specializedConstructor);
                }
                if (typeof(ISupportInitialize).IsAssignableFrom(targetType))
                {
                    il.EmitCall(OpCodes.Callvirt, typeof(ISupportInitialize).GetMethod("EndInit"), null);
                }
            }
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ret);
            return (Func<IDataReader, T>)dm.CreateDelegate(typeof(Func<IDataReader, T>));
        }

        internal static MethodInfo GetPropertySetter(PropertyInfo propertyInfo, Type type)
        {
            return propertyInfo.DeclaringType == type ?
                propertyInfo.GetSetMethod(true) :
                propertyInfo.DeclaringType.GetProperty(
                   propertyInfo.Name,
                   BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                   Type.DefaultBinder,
                   propertyInfo.PropertyType,
                   propertyInfo.GetIndexParameters().Select(p => p.ParameterType).ToArray(),
                   null).GetSetMethod(true);
        }

        private static void LoadLocalAddress(ILGenerator il, int index)
        {
            if (index < 0 || index >= short.MaxValue) throw new ArgumentNullException("index");

            if (index <= 255)
            {
                il.Emit(OpCodes.Ldloca_S, (byte)index);
            }
            else
            {
                il.Emit(OpCodes.Ldloca, (short)index);
            }
        }

        private static ConstructorInfo GetConstructor(Type targetType)
        {
            var constructors = targetType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(c => c.IsPublic ? 0 : (c.IsPrivate ? 2 : 1)).ThenBy(c => c.GetParameters().Length);

            ConstructorInfo defaultContructor = constructors.FirstOrDefault(c => c.GetParameters().Count() == 0);
            if (defaultContructor != null) return defaultContructor;
            return null;
        }

        private static void EmitInt32(ILGenerator il, int value)
        {
            switch (value)
            {
                case -1: il.Emit(OpCodes.Ldc_I4_M1); break;
                case 0: il.Emit(OpCodes.Ldc_I4_0); break;
                case 1: il.Emit(OpCodes.Ldc_I4_1); break;
                case 2: il.Emit(OpCodes.Ldc_I4_2); break;
                case 3: il.Emit(OpCodes.Ldc_I4_3); break;
                case 4: il.Emit(OpCodes.Ldc_I4_4); break;
                case 5: il.Emit(OpCodes.Ldc_I4_5); break;
                case 6: il.Emit(OpCodes.Ldc_I4_6); break;
                case 7: il.Emit(OpCodes.Ldc_I4_7); break;
                case 8: il.Emit(OpCodes.Ldc_I4_8); break;
                default:
                    if (value >= -128 && value <= 127)
                    {
                        il.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
                    }
                    else
                    {
                        il.Emit(OpCodes.Ldc_I4, value);
                    }
                    break;
            }
        }

        private static void LoadLocal(ILGenerator il, int index)
        {
            if (index < 0 || index >= short.MaxValue) throw new ArgumentNullException("index");
            switch (index)
            {
                case 0: il.Emit(OpCodes.Ldloc_0); break;
                case 1: il.Emit(OpCodes.Ldloc_1); break;
                case 2: il.Emit(OpCodes.Ldloc_2); break;
                case 3: il.Emit(OpCodes.Ldloc_3); break;
                default:
                    if (index <= 255)
                    {
                        il.Emit(OpCodes.Ldloc_S, (byte)index);
                    }
                    else
                    {
                        il.Emit(OpCodes.Ldloc, (short)index);
                    }
                    break;
            }
        }
        private static void StoreLocal(ILGenerator il, int index)
        {
            if (index < 0 || index >= short.MaxValue) throw new ArgumentNullException("index");
            switch (index)
            {
                case 0: il.Emit(OpCodes.Stloc_0); break;
                case 1: il.Emit(OpCodes.Stloc_1); break;
                case 2: il.Emit(OpCodes.Stloc_2); break;
                case 3: il.Emit(OpCodes.Stloc_3); break;
                default:
                    if (index <= 255)
                    {
                        il.Emit(OpCodes.Stloc_S, (byte)index);
                    }
                    else
                    {
                        il.Emit(OpCodes.Stloc, (short)index);
                    }
                    break;
            }
        }
        class ColumnInfo
        {
            public int Index { get; set; }
            public string Name { get; set; }
            public Type Type { get; set; }

            public Expression Reader { get; set; }
        }
        class TPropertyInfo
        {
            public PropertyInfo Info { get; set; }
            public Expression Expression { get; set; }
        }
    }
}
