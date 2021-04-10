using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace SimpleNet
{
	public class ByteUtils
	{
		public static T BytesToObject<T>(byte[] bytes)
		{
			using (var memStream = new MemoryStream(bytes))
			{
				var binForm = new BinaryFormatter();
				T obj = (T) binForm.Deserialize(memStream);
				return obj;
			}
		}

		public static byte[] ObjectToBytes<T>(object obj)
		{
			BinaryFormatter bf = new BinaryFormatter();
			using (var ms = new MemoryStream())
			{
				bf.Serialize(ms, obj);
				return ms.ToArray();
			}
		}

		public static T BytesToStruct<T>(byte[] bytes) where T : struct
		{
			GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
			try
			{
				T t = (T) Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
				return t;
			}
			finally
			{
				handle.Free();
			}
		}
		
		public static byte[] StructToBytes<T>(ref T t) where T : struct
		{
			int len = Marshal.SizeOf(t);

			byte[] arr = new byte[len];

			IntPtr ptr = Marshal.AllocHGlobal(len);

			Marshal.StructureToPtr(t, ptr, true);

			Marshal.Copy(ptr, arr, 0, len);

			Marshal.FreeHGlobal(ptr);

			return arr;
		}

		public static string BytesToString(byte[] buffer)
		{
			return Encoding.ASCII.GetString(buffer);
		}

		public static byte[] StringToBytes(string s)
		{
			return Encoding.ASCII.GetBytes(s);
		}

		public static byte[] GetBytes(IntPtr ptr, int start, int length)
		{
			byte[] buffer = new byte[length];
			Marshal.Copy((ptr + start), buffer, 0, length);
			return buffer;
		}

		public static byte ReadByte(IntPtr ptr, int index)
		{
			return Marshal.ReadByte(ptr, index);
		}
	}
}