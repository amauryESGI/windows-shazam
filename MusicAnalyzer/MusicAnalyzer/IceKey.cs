using System;
using System.Text;

namespace MusicAnalyzer {
    public class IceKey {
        /* ATTENTION!
         * This class was decompiles by ILSpy, a .NET decompiler which is
         * quite bad in terms of names of stuff, so the original varibales
         * name were diffrent. Anyway, it was used due to "unsafe" errors.
         */

        private int _size;
        private int _rounds;
        private int[,] _keySchedule;
        private static ulong[,] _spBox;
        private static bool _spBoxInitialised;
        private static int[,] _sMod = {
			
			{
				333,
				313,
				505,
				369
			},
			
			{
				379,
				375,
				319,
				391
			},
			
			{
				361,
				445,
				451,
				397
			},
			
			{
				397,
				425,
				395,
				505
			}
		};

        private static int[,] _sXor = {
			
			{
				131,
				133,
				155,
				205
			},
			
			{
				204,
				167,
				173,
				65
			},
			
			{
				75,
				46,
				212,
				51
			},
			
			{
				234,
				203,
				46,
				4
			}
		};

        private static uint[] _pBox = {
			1u,
			128u,
			1024u,
			8192u,
			524288u,
			2097152u,
			16777216u,
			1073741824u,
			8u,
			32u,
			256u,
			16384u,
			65536u,
			8388608u,
			67108864u,
			536870912u,
			4u,
			16u,
			512u,
			32768u,
			131072u,
			4194304u,
			134217728u,
			268435456u,
			2u,
			64u,
			2048u,
			4096u,
			262144u,
			1048576u,
			33554432u,
			2147483648u
		};

        private static int[] _keyrot = {
			0,
			1,
			2,
			3,
			2,
			1,
			3,
			0,
			1,
			3,
			2,
			0,
			3,
			1,
			0,
			2
		};

        private int gf_mult(int a, int b, int m) {
            int num = 0;
            while (b != 0) {
                if ((b & 1) != 0) {
                    num ^= a;
                }
                a <<= 1;
                b >>= 1;
                if (a >= 256) {
                    a ^= m;
                }
            }
            return num;
        }

        private long gf_exp7(int b, int m) {
            if (b == 0) {
                return 0L;
            }
            int num = gf_mult(b, b, m);
            num = gf_mult(b, num, m);
            num = gf_mult(num, num, m);
            return gf_mult(b, num, m);
        }

        private long Perm32(long x) {
            long num = 0L;
            long num2 = 0L;
            while (x != 0L) {
                if ((x & 1L) != 0L) {
                    num |= _pBox[(int)checked((IntPtr)num2)];
                }
                num2 += 1L;
                x >>= 1;
            }
            return num;
        }

        private void SpBoxInit() {
            _spBox = new ulong[4, 1024];
            for (int i = 0; i < 1024; i++) {
                int num = i >> 1 & 255;
                int num2 = (i & 1) | (i & 512) >> 8;
                long x = gf_exp7(num ^ _sXor[0, num2], _sMod[0, num2]) << 24;
                _spBox[0, i] = (ulong)Perm32(x);
                x = gf_exp7(num ^ _sXor[1, num2], _sMod[1, num2]) << 16;
                _spBox[1, i] = (ulong)Perm32(x);
                x = gf_exp7(num ^ _sXor[2, num2], _sMod[2, num2]) << 8;
                _spBox[2, i] = (ulong)Perm32(x);
                x = gf_exp7(num ^ _sXor[3, num2], _sMod[3, num2]);
                _spBox[3, i] = (ulong)Perm32(x);
            }
        }

        public IceKey(int level) {
            if (!_spBoxInitialised) {
                SpBoxInit();
                _spBoxInitialised = true;
            }
            if (level < 1) {
                _size = 1;
                _rounds = 8;
            } else {
                _size = level;
                _rounds = level * 16;
            }
            _keySchedule = new int[_rounds, 3];
        }

        private void ScheduleBuild(int[] kb, int n, int krotIdx) {
            for (int i = 0; i < 8; i++) {
                int num = _keyrot[krotIdx + i];
                for (int j = 0; j < 3; j++) {
                    _keySchedule[n + i, j] = 0;
                }
                for (int j = 0; j < 15; j++) {
                    int num2 = j % 3;
                    for (int k = 0; k < 4; k++) {
                        int num3 = kb[num + k & 3];
                        int num4 = num3 & 1;
                        _keySchedule[n + i, num2] = (_keySchedule[n + i, num2] << 1 | num4);
                        kb[num + k & 3] = (num3 >> 1 | (num4 ^ 1) << 15);
                    }
                }
            }
        }

        public void Set(char[] key) {
            int[] array = new int[4];
            if (_rounds == 8) {
                for (int i = 0; i < 4; i++) {
                    array[3 - i] = (key[i * 2] & 'ÿ') << 8 | (key[i * 2 + 1] & 'ÿ');
                }
                ScheduleBuild(array, 0, 0);
                return;
            }
            for (int i = 0; i < _size; i++) {
                for (int j = 0; j < 4; j++) {
                    array[3 - j] = key[i * 8 + j * 2] << 8 | key[i * 8 + j * 2 + 1];
                }
                ScheduleBuild(array, i * 8, 0);
                ScheduleBuild(array, _rounds - 8 - i * 8, 8);
            }
        }

        public void Clear() {
            for (int i = 0; i < _rounds; i++) {
                for (int j = 0; j < 3; j++) {
                    _keySchedule[i, j] = 0;
                }
            }
        }

        private ulong RoundFunc(ulong p, int i, int[,] subkey) {
            ulong num = (p >> 16 & 1023uL) | ((p >> 14 | p << 18) & 1047552uL);
            ulong num2 = (p & 1023uL) | (p << 2 & 1047552uL);
            ulong num3 = (ulong)(subkey[i, 2] & (long)(num ^ num2));
            ulong num4 = num3 ^ num2;
            num3 ^= num;
            num3 ^= (ulong)subkey[i, 0];
            num4 ^= (ulong)subkey[i, 1];
            return checked(_spBox[(int)((IntPtr)0L), (int)((IntPtr)(num3 >> 10))] | _spBox[(int)((IntPtr)1L), (int)((IntPtr)(num3 & 1023uL))] | _spBox[(int)((IntPtr)2L), (int)((IntPtr)(num4 >> 10))] | _spBox[(int)((IntPtr)3L), (int)((IntPtr)(num4 & 1023uL))]);
        }

        private void Encrypt(byte[] plaintext, byte[] ciphertext, int idx) {
            ulong num = (ulong)plaintext[idx] << 24 | (ulong)plaintext[idx + 1] << 16 | (ulong)plaintext[idx + 2] << 8 | plaintext[idx + 3];
            ulong num2 = (ulong)plaintext[idx + 4] << 24 | (ulong)plaintext[idx + 5] << 16 | (ulong)plaintext[idx + 6] << 8 | plaintext[idx + 7];
            for (int i = 0; i < _rounds; i += 2) {
                num ^= RoundFunc(num2, i, _keySchedule);
                num2 ^= RoundFunc(num, i + 1, _keySchedule);
            }
            for (int i = 0; i < 4; i++) {
                ciphertext[idx + 3 - i] = (byte)(num2 & 255uL);
                ciphertext[idx + 7 - i] = (byte)(num & 255uL);
                num2 >>= 8;
                num >>= 8;
            }
        }

        private void Decrypt(byte[] ciphertext, byte[] plaintext) {
            ulong num = (ulong)ciphertext[0] << 24 | (ulong)ciphertext[1] << 16 | (ulong)ciphertext[2] << 8 | ciphertext[3];
            ulong num2 = (ulong)ciphertext[4] << 24 | (ulong)ciphertext[5] << 16 | (ulong)ciphertext[6] << 8 | ciphertext[7];
            for (int i = _rounds - 1; i > 0; i -= 2) {
                num ^= RoundFunc(num2, i, _keySchedule);
                num2 ^= RoundFunc(num, i - 1, _keySchedule);
            }
            for (int i = 0; i < 4; i++) {
                plaintext[3 - i] = (byte)(num2 & 255uL);
                plaintext[7 - i] = (byte)(num & 255uL);
                num2 >>= 8;
                num >>= 8;
            }
        }

        public int KeySize() {
            return _size * 8;
        }

        public int BlockSize() {
            return 8;
        }

        public char[] EncString(string str) {
            char[] array = str.ToCharArray();
            int length = str.Length;
            int num = (length / 8 + 1) * 8;
            byte[] array2 = new byte[num];
            byte[] array3 = new byte[num];
            for (int i = 0; i < num; i++) {
                array2[i] = 0;
            }
            for (int j = 0; j < length; j++) {
                array2[j] = (byte)array[j];
            }
            for (int k = 0; k < num; k += 8) {
                Encrypt(array2, array3, k);
            }
            string text = "#0x";
            for (int l = 0; l < num; l++) {
                int num2 = array3[l];
                text += string.Format("{0:x2}", new object[]
				{
					Convert.ToUInt32(num2.ToString())
				});
            }
            return text.ToCharArray();
        }

        public byte[] EncBinary(byte[] data, int dataSize) {
            int num = (dataSize / 8 + 1) * 8;
            byte[] array = new byte[num];
            byte[] array2 = new byte[num];
            for (int i = 0; i < dataSize; i++) {
                array[i] = data[i];
            }
            for (int j = 0; j < num; j += 8) {
                Encrypt(array, array2, j);
            }
            return array2;
        }

        public string DecString(string str) {
            str = str.Substring("#0x".Length);
            StringBuilder stringBuilder = new StringBuilder();
            char[] array = str.ToCharArray();
            int num = array.Length;
            for (int i = 0; i < num; i += 16) {
                byte[] array2 = new byte[8];
                byte[] array3 = new byte[8];
                for (int j = 0; j < 8; j++) {
                    array2[j] = Convert.ToByte(String.Concat(array[i + j * 2], array[i + j * 2 + 1]), 16);
                }
                Decrypt(array2, array3);
                for (int k = 0; k < 8; k++) {
                    if (array3[k] != 0) {
                        stringBuilder.Append(Convert.ToChar(array3[k]));
                    }
                }
            }
            return stringBuilder.ToString();
        }
    }
}
