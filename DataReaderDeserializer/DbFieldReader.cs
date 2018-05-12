using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
namespace DataReaderDeserializer
{
    public class DbFieldReader:FieldReader
    {
        IDataReader reader;
        public DbFieldReader(IDataReader reader)
        {
            this.reader = reader;
            this.Init();
        }

        protected override int FieldCount
        {
            get { return this.reader.FieldCount; }
        }

        protected override Type GetFieldType(int ordinal)
        {
            return this.reader.GetFieldType(ordinal);
        }

        protected override bool IsDBNull(int ordinal)
        {
            return this.reader.IsDBNull(ordinal);
        }

        protected override T GetValue<T>(int ordinal)
        {
            return (T)this.Convert(this.reader.GetValue(ordinal), typeof(T));
        }

        protected override Byte GetByte(int ordinal)
        {
            return this.reader.GetByte(ordinal);
        }

        protected override Char GetChar(int ordinal)
        {
            return this.reader.GetChar(ordinal);
        }

        protected override DateTime GetDateTime(int ordinal)
        {
            return this.reader.GetDateTime(ordinal);
        }

        protected override Decimal GetDecimal(int ordinal)
        {
            return this.reader.GetDecimal(ordinal);
        }

        protected override Double GetDouble(int ordinal)
        {
            return this.reader.GetDouble(ordinal);
        }

        protected override Single GetSingle(int ordinal)
        {
            return this.reader.GetFloat(ordinal);
        }

        protected override Guid GetGuid(int ordinal)
        {
            return this.reader.GetGuid(ordinal);
        }

        protected override Int16 GetInt16(int ordinal)
        {
            return this.reader.GetInt16(ordinal);
        }

        protected override Int32 GetInt32(int ordinal)
        {
            return this.reader.GetInt32(ordinal);
        }

        protected override Int64 GetInt64(int ordinal)
        {
            return this.reader.GetInt64(ordinal);
        }

        protected override String GetString(int ordinal)
        {
            return this.reader.GetString(ordinal);
        }
        private  object Convert(object value, Type type)
        {
            if (value == null)
            {
                return TypeHelper.GetDefault(type);
            }
            type = TypeHelper.GetNonNullableType(type);
            Type vtype = value.GetType();
            if (type != vtype)
            {
                if (type.IsEnum)
                {
                    if (vtype == typeof(string))
                    {
                        return Enum.Parse(type, (string)value);
                    }
                    else
                    {
                        Type utype = Enum.GetUnderlyingType(type);
                        if (utype != vtype)
                        {
                            value = System.Convert.ChangeType(value, utype);
                        }
                        return Enum.ToObject(type, value);
                    }
                }
                return System.Convert.ChangeType(value, type);
            }
            return value;
        }

    }
}
