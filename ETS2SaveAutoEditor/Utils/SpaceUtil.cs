using ETS2SaveAutoEditor.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ASE.Utils {
    public class Quaternion(double w, double x, double y, double z) {
        public double w = w, x = x, y = y, z = z;

        // Background knowledges

        // Rotation: The action of rotating around an axis or center.
        // Right-hand rule: A rule that defines the direction of rotation. If you point your right thumb in the direction of the axis of rotation, the curl of your fingers gives the direction of rotation. Our system uses the right-hand rule.


        // Euler angles: A set of three angles used to describe the orientation of an object in 3D space. The angles are typically denoted as 𝜓 (yaw, phi), 𝜃 (pitch, theta), and 𝜆 (roll, psi).
        // While it's intuitive, it suffers from gimbal lock, where two of the three axes become aligned, leading to a loss of one degree of freedom, and also costly in terms of computation.

        // Axis-angle representation: A rotation is represented by an angle 𝜃 and an axis of rotation. The axis is a unit vector, and the angle is the amount of rotation around the axis.
        // This is enough to represent any rotation in 3D space.

        // Quaternion: A four-dimensional number that can be used to represent rotations in 3D space. It is a more compact representation than Euler angles and avoids gimbal lock. A quaternion is defined as 𝑞 = 𝑤 + 𝑥𝑖 + 𝑦𝑗 + 𝑧𝑘, where 𝑤, 𝑥, 𝑦, 𝑧 are real numbers and 𝑖, 𝑗, 𝑘 are imaginary units. The quaternion must be normalized to represent a rotation.
        // In quaternion, x, y, z are multiplications of sin(𝜃/2) and components of the unit vector of the axis of rotation, and w is cos(𝜃/2).
        // They can be easily calculated in computers and are used in computer graphics and robotics.


        // Intrinsic Rotation: You rotate the object about its own axes, which change after each rotation. This is the default rotation system in most 3D software.
        // Extrinsic Rotation: You rotate the object about the axes of the world, which remain fixed after each rotation.

        // An intrinsic rotation sequence about the 𝑥, 𝑦, 𝑧 axes is mathematically equivalent to an extrinsic rotation sequence about the 𝑧, 𝑦, 𝑥 axes, in that order.
        // This equivalence arises due to the way rotation matrices are applied and multiplied in each case.

        // https://chatgpt.com/share/6742c81d-94c8-8010-89f9-acf34f370719
        // A rotation in the order of 𝑥, 𝑦, 𝑧 axes is:
        // - If it's intrinsic rotation:
        // -- R = Rx * Ry' * Rz'' and Q = Qx * Qy * Qz
        // - If it's extrinsic rotation:
        // -- R = Rz * Ry * Rx and Q = Qz * Qy * Qx
        // Rx, Ry, Rz are rotation matrices representing rotation about x, y, z axes respectively.
        // Qx, Qy, Qz are quaternions representing rotation about x, y, z axes respectively.
        // This formula has been verified by gpt-o1. However, the formula is not verified by a human expert.
        // https://chatgpt.com/share/673895f4-ebac-8010-af8f-fbb5f7c1958d

        // https://dominicplein.medium.com/extrinsic-intrinsic-rotation-do-i-multiply-from-right-or-left-357c38c1abfd

        // In ETS2, the rotation is applied in y, x, z order in an intrinsic manner.


        // Most formulas are given by AI.



        private static double NormalizeAngle(double angle) {
            angle %= 2 * Math.PI;
            if (angle > Math.PI) angle -= 2 * Math.PI;
            else if (angle < -Math.PI) angle += 2 * Math.PI;
            return angle;
        }

        public static Quaternion FromEuler(double yaw, double pitch, double roll) {
            // Normalize angles to the range -π to π for consistency
            //yaw = NormalizeAngle(yaw);
            //pitch = NormalizeAngle(pitch);
            //roll = NormalizeAngle(roll);

            // Generate quaternions for each axis-angle rotation
            Quaternion qYaw = new Vector3(0, 1, 0).AsAxisAngle(yaw);   // Yaw about positive Y-axis
            Quaternion qPitch = new Vector3(1, 0, 0).AsAxisAngle(pitch); // Pitch about positive X-axis
            Quaternion qRoll = new Vector3(0, 0, 1).AsAxisAngle(roll); // Roll about positive Z-axis

            // yaw - pitch - roll intrinsic rotation.
            return qYaw * qPitch * qRoll;
        }


        public static Quaternion FromEulerDegrees(double yaw, double pitch, double roll) {
            return FromEuler(yaw * Math.PI / 180, pitch * Math.PI / 180, roll * Math.PI / 180);
        }

        public (double yaw, double pitch, double roll) ToEuler() {
            // Before getting started, let's unify the unit of rotation angles to radians.
            // 
            // A rotation matrix of the quaternion is, regardless of characteristics of Euler angles, defined as follows:
            // This assumes the quaternion is normalized.
            //
            //      | 1 - 2(y^2 + z^2)  2(xy - wz)        2(xz + wy)       |
            // Rq = | 2(xy + wz)        1 - 2(x^2 + z^2)  2(yz - wx)       |
            //      | 2(xz - wy)        2(yz + wx)        1 - 2(x^2 + y^2) |
            //
            // Source: https://en.wikipedia.org/wiki/Quaternions_and_spatial_rotation#Quaternion-derived_rotation_matrix (accessed 2024-11-16)
            // 
            // From now on, we'll refer to each element of the matrix as Rij, where i is the row number and j is the column number.
            //
            //
            // In our scenario, this intrinsic rotation is applied in the order of y, x, z.
            // ∴ The rotation matrix R = Ry(𝜃y) * Rx(𝜃x) * Rz(𝜃z)
            // NOTE: Roll is done about the NEGATIVE z-axis.

            //          |  1        0        0        |
            // Rx(𝜃x) = |  0        cos(𝜃x) -sin(𝜃x)  |
            //          |  0        sin(𝜃x)  cos(𝜃x)  |

            //          |  cos(𝜃y)  0        sin(𝜃y)  |
            // Ry(𝜃y) = |  0        1        0        |
            //          | -sin(𝜃y)  0        cos(𝜃y)  |

            //          |  cos(𝜃z) -sin(𝜃z)  0        |
            // Rz(𝜃z) = |  sin(𝜃z)  cos(𝜃z)  0        |
            //          |  0        0        1        |

            //          |   cos(𝜃y)  sin(𝜃x)sin(𝜃y)  cos(𝜃x)sin(𝜃y) |
            // Ry*Rx =  |   0        cos(𝜃x)        -sin(𝜃x)        |
            //          |  -sin(𝜃y)  sin(𝜃x)cos(𝜃y)  cos(𝜃x)cos(𝜃y) |

            //                  |  cos(𝜃y)cos(𝜃z)+sin(𝜃x)sin(𝜃y)sin(𝜃z) -cos(𝜃y)sin(𝜃z)+sin(𝜃x)sin(𝜃y)cos(𝜃z)  cos(𝜃x)sin(𝜃y) |
            // R = (Ry*Rx)*Rz = |  cos(𝜃x)sin(𝜃z)                        cos(𝜃x)cos(𝜃z)                       -sin(𝜃x)        |
            //                  | -sin(𝜃y)cos(𝜃z)+sin(𝜃x)cos(𝜃y)sin(𝜃z)  sin(𝜃y)sin(𝜃z)+sin(𝜃x)cos(𝜃y)cos(𝜃z)  cos(𝜃x)cos(𝜃y) |

            // where R = Rq

            // 𝜃x = arcsin ( -R12 )
            // 𝜃y = arctan2 ( R02, R22 )
            // 𝜃z = arctan2 ( R10, R11 )

            // I have verified the formula above myself, which was originally given by AI. Time to implement it.

            var q = Normalize();
            var w = q.w; var x = q.x; var y = q.y; var z = q.z;

            //      | 1 - 2(y^2 + z^2)  2(xy - wz)        2(xz + wy)       |
            // Rq = | 2(xy + wz)        1 - 2(x^2 + z^2)  2(yz - wx)       |
            //      | 2(xz - wy)        2(yz + wx)        1 - 2(x^2 + y^2) |
            // Be careful. Copilot for C# is very stupid and produces wrong results here.
            double[,] Rq = {{1 - 2 * (y * y + z * z), 2 * (x * y - w * z), 2 * (x * z + w * y) },
                             { 2 * (x * y + w * z), 1 - 2 * (x * x + z * z), 2 * (y * z - w * x)},
                             { 2 * (x * z - w * y), 2 * (y * z + w * x), 1 - 2 * (x * x + y * y) } };

            // If values are extremely close to -1, 0, 1, assume they're integer.
            for (int row = 0; row < 3; row++) {
                for (int col = 0; col < 3; col++) {
                    if (Math.Abs(Rq[row, col] - Math.Round(Rq[row, col])) < 1e-12) {
                        Rq[row, col] = Math.Round(Rq[row, col]);
                        if (Rq[row, col] == -0) Rq[row, col] = 0;
                    }
                }
            }


            // Print the Rq matrix fancy with indents
            //for (int i = 0; i < Rq.GetLength(0); i++) {
            //    for (int j = 0; j < Rq.GetLength(1); j++) {
            //        // Modify Rq to the rounded value if the error's less than 1e-12
            //        Console.Write($"{Rq[i, j],30}");
            //    }
            //    Console.WriteLine();
            //}

            // Calculation of angles
            var theta_x = Math.Asin(-Rq[1, 2]);
            var theta_y = Math.Atan2(Rq[0, 2], Rq[2, 2]);
            var theta_z = Math.Atan2(Rq[1, 0], Rq[1, 1]);

            // Needs special handling when cos(𝜃x) = 0 (gimbal lock)
            var sx = -Rq[1, 2];
            if (sx == 1) {
                // R00 =  cos(𝜃y)cos(𝜃z) + sin(𝜃y)sin(𝜃z) = cos(𝜃z - 𝜃y)
                // R20 = -sin(𝜃y)cos(𝜃z) + cos(𝜃y)sin(𝜃z) = sin(𝜃z - 𝜃y)
                // R21 =  sin(𝜃y)sin(𝜃z) + cos(𝜃y)cos(𝜃z) = cos(𝜃y - 𝜃z) =  cos(𝜃z - 𝜃y)
                // R01 = -cos(𝜃y)sin(𝜃z) + sin(𝜃y)cos(𝜃z) = sin(𝜃y - 𝜃z) = -sin(𝜃z - 𝜃y)

                // Print values of atan2(R20, R00), atan2(-R01, R21)
                //Console.WriteLine("90");
                //Console.WriteLine($"{Math.Atan2(Rq[2, 0], Rq[0, 0]) * 180 / Math.PI}\n{Math.Atan2(-Rq[0, 1], Rq[2, 1]) * 180 / Math.PI}");

                // a = atan2(R20, R00)
                // 𝜃z and 𝜃y can be any value that satisfies the equation 𝜃z - 𝜃y = a
                // Let's assume 𝜃y = 0, then 𝜃z = a
                theta_y = 0;
                theta_z = Math.Atan2(Rq[2, 0], Rq[0, 0]);
            } else if (sx == -1) {
                // R00 =  cos(𝜃y)cos(𝜃z) - sin(𝜃y)sin(𝜃z) = cos(𝜃z + 𝜃y)
                // R20 = -sin(𝜃y)cos(𝜃z) - cos(𝜃y)sin(𝜃z) = -sin(𝜃z + 𝜃y)
                // R21 =  sin(𝜃y)sin(𝜃z) - cos(𝜃y)cos(𝜃z) = -cos(𝜃y + 𝜃z)
                // R01 = -cos(𝜃y)sin(𝜃z) - sin(𝜃y)cos(𝜃z) = -sin(𝜃y + 𝜃z)

                // Print values of atan2(-R20, R00), atan2(-R01, -R21)
                //Console.WriteLine("-90");
                //Console.WriteLine($"{Math.Atan2(-Rq[2, 0], Rq[0, 0]) * 180 / Math.PI}\n{Math.Atan2(-Rq[0, 1], -Rq[2, 1]) * 180 / Math.PI}");

                // a = atan2(-R20, R00)
                // 𝜃z and 𝜃y can be any value that satisfies the equation 𝜃z + 𝜃y = a
                // Let's assume 𝜃y = 0, then 𝜃z = a
                theta_y = 0;
                theta_z = Math.Atan2(-Rq[2, 0], Rq[0, 0]);
            }

            // x, y, z correspond to pitch, yaw, roll respectively.
            return (theta_y, theta_x, theta_z);
        }

        public (double yaw, double pitch, double roll) ToEulerDegrees() {
            var (yaw, pitch, roll) = ToEuler();
            return (yaw * 180 / Math.PI, pitch * 180 / Math.PI, roll * 180 / Math.PI);
        }

        public static Quaternion FromVector(double x, double y, double z) {
            return new Quaternion(0, x, y, z);
        }

        public static Quaternion Parse(string value) {
            ArgumentNullException.ThrowIfNull(value);

            // (w; x, y, z)
            // w can be either decimal or IEEE 754 floating-point number.
            // If it's IEEE 754, it's can be either 4 bytes or 8 bytes. Hexadecimal representation starts with & followed by 8 or 16 characters of [0-9A-F].

            // First, throw if the string is not in the correct format.
            var pattern = new Regex(@"^\((?<w>[^;]+); (?<x>[^,]+), (?<y>[^,]+), (?<z>[^,]+)\)$");
            var match = pattern.Match(value);
            if (!match.Success) {
                throw new FormatException("The string is not in the correct format.");
            }

            var w = SCSDecimalParser.ParseDecimal(match.Groups["w"].Value);
            var x = SCSDecimalParser.ParseDecimal(match.Groups["x"].Value);
            var y = SCSDecimalParser.ParseDecimal(match.Groups["y"].Value);
            var z = SCSDecimalParser.ParseDecimal(match.Groups["z"].Value);
            return new Quaternion(w, x, y, z);
        }

        public static bool TryParse(string value, [NotNullWhen(true)] out Quaternion? result) {
            try {
                result = Parse(value);
                return true;
            } catch {
                result = null;
                return false;
            }
        }

        // Conjugate of the quaternion
        public Quaternion Conjugate() => new(w, -x, -y, -z);

        // Normalize the quaternion
        public Quaternion Normalize() {
            double length = Math.Sqrt(w * w + x * x + y * y + z * z);
            if (length == 0) throw new InvalidOperationException("Cannot normalize a zero quaternion.");
            return new Quaternion(w / length, x / length, y / length, z / length);
        }

        public Quaternion RotationFrom(Quaternion a) {
            var B = Normalize();
            var A = a.Normalize();

            // B - A
            var diff = B * A.Conjugate();

            return diff.Normalize();
        }

        public static Quaternion operator +(Quaternion a) => a;
        public static Quaternion operator -(Quaternion a) => new(-a.w, -a.x, -a.y, -a.z);

        public static Quaternion operator +(Quaternion left, Quaternion right) {
            return new Quaternion(left.w + right.w, left.x + right.x, left.y + right.y, left.z + right.z);
        }

        public static Quaternion operator *(Quaternion left, Quaternion right) {
            return new Quaternion(
                left.w * right.w - left.x * right.x - left.y * right.y - left.z * right.z,
                left.w * right.x + left.x * right.w + left.y * right.z - left.z * right.y,
                left.w * right.y - left.x * right.z + left.y * right.w + left.z * right.x,
                left.w * right.z + left.x * right.y - left.y * right.x + left.z * right.w
            );
        }

        public static Vector3 operator *(Quaternion rotation, Vector3 point) {
            Quaternion pointQuat = new Quaternion(0, point.x, point.y, point.z);
            Quaternion rotatedQuat = (rotation * pointQuat) * rotation.Normalize().Conjugate();
            return new Vector3(rotatedQuat.x, rotatedQuat.y, rotatedQuat.z);
        }

        public Vector3 GetPitchAxis() {
            return this * new Vector3(1, 0, 0);
        }

        public Vector3 GetYawAxis() {
            return this * new Vector3(0, 1, 0);
        }

        public Vector3 GetRollAxis() {
            return this * new Vector3(0, 0, 1);
        }

        public Vector3 GetDirection() { // In ETS2, the forward direction is the negative Z-axis.
            return this * new Vector3(0, 0, -1);
        }

        public (Vector3 Axis, double Angle) Decomposite() {
            // w is cosineHalfAngle, (x, y, z) is sinHalfAngle * axis
            double angle = 2 * Math.Acos(w);
            double sinHalfAngle = Math.Sqrt(1 - w * w);

            if (sinHalfAngle < 1e-10) {
                return (new Vector3(0, 0, 0).Normalize(), 0);
            } else {
                return (new Vector3(x / sinHalfAngle, y / sinHalfAngle, z / sinHalfAngle).Normalize(), angle);
            }
        }

        public override string ToString() {
            return $"({w}; {x}, {y}, {z})";
        }

        public string ToAxisAngleString() {
            var (axis, angle) = Decomposite();
            return $"{axis} {angle * 180 / Math.PI}";
        }
    }

    public class Vector3(double x, double y, double z) {
        public double x = x, y = y, z = z;

        public static Vector3 Parse(string value) {
            var pattern = new Regex(@"^\(([^,]+), ([^,]+), ([^,]+)\)$");
            var match = pattern.Match(value);
            if (!match.Success) {
                throw new FormatException("The string is not in the correct format.");
            }

            var x = SCSDecimalParser.ParseDecimal(match.Groups[1].Value);
            var y = SCSDecimalParser.ParseDecimal(match.Groups[2].Value);
            var z = SCSDecimalParser.ParseDecimal(match.Groups[3].Value);
            return new Vector3(x, y, z);
        }

        public static bool TryParse(string value, [NotNullWhen(true)] out Vector3? result) {
            try {
                result = Parse(value);
                return true;
            } catch {
                result = null;
                return false;
            }
        }

        public static Vector3 operator +(Vector3 left, Vector3 right) {
            return new Vector3(left.x + right.x, left.y + right.y, left.z + right.z);
        }

        public static Vector3 operator -(Vector3 left, Vector3 right) {
            return new Vector3(left.x - right.x, left.y - right.y, left.z - right.z);
        }

        public Vector3 Normalize() {
            double length = Math.Sqrt(x * x + y * y + z * z);
            if (length == 0) return new Vector3(0, 0, 0);
            return new Vector3(x / length, y / length, z / length);
        }

        public static Vector3 Cross(Vector3 a, Vector3 b) {
            return new Vector3(
                a.y * b.z - a.z * b.y,
                a.z * b.x - a.x * b.z,
                a.x * b.y - a.y * b.x
            );
        }

        public static double Dot(Vector3 a, Vector3 b) {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        /// <summary>
        /// Gets the quaternion of axis-angle representation of the rotation.
        /// </summary>
        /// <param name="radians"></param>
        /// <returns></returns>
        public Quaternion AsAxisAngle(double radians) {
            double halfAngle = radians / 2;
            double sinHalfAngle = Math.Sin(halfAngle);
            var normalized = Normalize();
            return new Quaternion(Math.Cos(halfAngle), normalized.x * sinHalfAngle, normalized.y * sinHalfAngle, normalized.z * sinHalfAngle);
        }

        public override string ToString() {
            return $"({x}, {y}, {z})";
        }
    }

    public class SCSPlacement(Vector3 position, Quaternion orientation) {
        public Vector3 Position = position;
        public Quaternion Orientation = orientation;

        public static SCSPlacement Parse(string value) {
            ArgumentNullException.ThrowIfNull(value);

            // (x, y, z) (w; x, y, z)
            // So we will split the string into two parts and parse them separately in Vector3 and Quaternion.

            int firstSegEnd = value.IndexOf(')');
            if (firstSegEnd == -1) {
                throw new FormatException("The string is not in the correct format.");
            }

            string firstSeg = value[0..(firstSegEnd + 1)];
            string secondSeg = value[(firstSegEnd + 2)..];

            return new SCSPlacement(Vector3.Parse(firstSeg), Quaternion.Parse(secondSeg));
        }

        public static bool TryParse(string value, [NotNullWhen(true)] out SCSPlacement? result) {
            try {
                result = Parse(value);
                return true;
            } catch {
                result = null;
                return false;
            }
        }

        public override string ToString() {
            return $"{Position} {Orientation}";
        }
    }

    public enum DecimalEncodingType {
        DecimalSingle,
        DecimalDouble,
        IEEE754Single,
        IEEE754Double,
        AutoSingle,
        AutoDouble
    }

    public class SCSDecimalParser {
        private static readonly Regex pattern1 = new(@"^[.\-0-9]+$");
        private static readonly Regex pattern2 = new(@"^&[0-9A-Fa-f]{8}$");
        private static readonly Regex pattern3 = new(@"^&[0-9A-Fa-f]{16}$");
        public static double ParseDecimal(string value) {
            ArgumentNullException.ThrowIfNull(value);

            // note that hex notations are always in big-endian.
            if (pattern1.IsMatch(value)) {
                return double.Parse(value); // will throw if the string is not in the correct format.
            } else if (pattern2.IsMatch(value)) {
                byte[] bytes = new byte[4];
                for (int i = 0; i < 4; i++) {
                    bytes[i] = byte.Parse(value.Substring(i * 2 + 1, 2), System.Globalization.NumberStyles.HexNumber);
                }
                return ByteEncoder.DecodeFloat(bytes, ByteOrder.BigEndian);
            } else if (pattern3.IsMatch(value)) {
                byte[] bytes = new byte[8];
                for (int i = 0; i < 8; i++) {
                    bytes[i] = byte.Parse(value.Substring(i * 2 + 1, 2), System.Globalization.NumberStyles.HexNumber);
                }
                return ByteEncoder.DecodeDouble(bytes, ByteOrder.BigEndian);
            } else {
                throw new FormatException("The string is not in the correct format.");
            }
        }

        public static bool TryParseDecimal(string value, [NotNullWhen(true)] out double? result) {
            try {
                result = ParseDecimal(value);
                return true;
            } catch {
                result = null;
                return false;
            }
        }

        public static string EncodeDecimal(double value, DecimalEncodingType? type) {
            if (type == null) {
                if (double.IsInfinity(value) || double.IsNaN(value)) {
                    type = DecimalEncodingType.IEEE754Single;
                } else if (value == (float)value) {
                    type = DecimalEncodingType.AutoSingle;
                } else {
                    type = DecimalEncodingType.AutoDouble;
                }
            }
            if (type == DecimalEncodingType.AutoSingle) {
                if (float.IsInteger((float)value)) {
                    type = DecimalEncodingType.DecimalSingle;
                } else {
                    type = DecimalEncodingType.IEEE754Single;
                }
            } else if (type == DecimalEncodingType.AutoDouble) {
                if (double.IsInteger(value)) {
                    type = DecimalEncodingType.DecimalDouble;
                } else {
                    type = DecimalEncodingType.IEEE754Double;
                }
            }
            switch (type) {
                case DecimalEncodingType.DecimalSingle:
                    return value.ToString();
                case DecimalEncodingType.DecimalDouble:
                    return value.ToString();
                case DecimalEncodingType.IEEE754Single:
                    byte[] bytes = ByteEncoder.EncodeFloat((float)value, ByteOrder.BigEndian);
                    return "&" + string.Join("", bytes.Select(b => b.ToString("X2")));
                case DecimalEncodingType.IEEE754Double:
                    bytes = ByteEncoder.EncodeDouble(value, ByteOrder.BigEndian);
                    return "&" + string.Join("", bytes.Select(b => b.ToString("X2")));
                default:
                    throw new ArgumentException("Invalid encoding type.");
            }
        }
    }
}