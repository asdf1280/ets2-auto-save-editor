using ASE.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Management.Deployment.Preview;

namespace ASE.Utils {
    public class QuaternionTester {
        public static void TestQuaternion() {
            // Define test cases with significant yaw, pitch, and roll values
            var testCases = new List<(double yaw, double pitch, double roll)>
            {
    // Zero rotation
    (0, 0, 0),
    
    // Single-axis rotations
    (90, 0, 0),
    (0, 90, 0),
    (0, 0, 90),
    (-90, 0, 0),
    (0, -90, 0),
    (0, 0, -90),
    
    // 180-degree rotations
    (180, 0, 0),
    (0, 180, 0),
    (0, 0, 180),
    (-180, 0, 0),
    (0, -180, 0),
    (0, 0, -180),
    
    // 270-degree rotations
    (270, 0, 0),
    (0, 270, 0),
    (0, 0, 270),
    (-270, 0, 0),
    (0, -270, 0),
    (0, 0, -270),
    
    // Full rotations
    (360, 0, 0),
    (0, 360, 0),
    (0, 0, 360),
    (-360, 0, 0),
    (0, -360, 0),
    (0, 0, -360),
    
    // Combined rotations
    (45, 45, 45),
    (-45, -45, -45),
    (30, 60, 90),
    (-30, -60, -90),
    (120, 240, 300),
    (-120, -240, -300),
    
    // Small angles
    (0.1, 0.2, 0.3),
    (-0.1, -0.2, -0.3),
    
    // Angles that may cause gimbal lock
    (90, 90, 90),
    (-90, -90, -90),
    (180, 90, 0),
    (180, -90, 0),
    
    // Random angles
    (15, 75, 105),
    (-15, -75, -105),
    (135, 225, 315),
    (-135, -225, -315),
};

            Console.WriteLine("Now performing quaternion case test. In this test, we will test the conversion between Euler angles and quaternions. If the result differs more than 1e-6 degrees, the test will fail.");
            // Iterate over the test cases and call debugQuat
            int failures = 0, count = 0;
            foreach (var (yaw, pitch, roll) in testCases) {
                Console.WriteLine("-------------------------------");
                count++;

                var quat = Quaternion.FromEulerDegrees(yaw, pitch, roll);
                var ed = quat.ToEulerDegrees();
                var quat2 = Quaternion.FromEulerDegrees(ed.Item1, ed.Item2, ed.Item3);

                Console.WriteLine($"Testing Yaw: {yaw}°, Pitch: {pitch}°, Roll: {roll}°");
                Console.WriteLine($"Quaternion: {quat.ToAxisAngleString()}");
                Console.WriteLine("Forward: " + quat.GetDirection());
                Console.WriteLine("Up: " + quat.GetYawAxis());

                var diff = quat.RotationFrom(quat2).Decomposite().Angle; // This will be positive because of how Decomposite works. It takes inverse of cosine and assumes it's positive.
                if (diff > 1e-6) {
                    Console.WriteLine($"Test failed. Difference: {diff * 180 / Math.PI} degrees.");
                    Console.WriteLine($"Euler angles (restored): {ed.Item1}, {ed.Item2}, {ed.Item3}");
                    Console.WriteLine($"Quaternion (reproduced): {quat2.ToAxisAngleString()}");

                    failures++;
                }
            }

            Console.WriteLine("-------------------------------");

            if (failures == 0) {
                Console.WriteLine("Passed all special quaternion conversion test cases!!!!!!!!!!");
            } else {
                Console.WriteLine("Failed " + failures + " out of " + count + " quaternion conversion test cases.");
            }

            {
                Console.WriteLine("Starting quaternion incremental test. The numbers should be increasing by 5 degrees from 0 to 180 then decreasing back to 0.");
                var random = new Random();
                var randomYaw = random.NextDouble() * 720 - 360;
                var randomPitch = random.NextDouble() * 720 - 360;
                Console.WriteLine("Random yaw and pitch: " + randomYaw + ", " + randomPitch);
                for (int i = 0; i <= 360; i += 5) {
                    var qa = Quaternion.FromEulerDegrees(randomYaw, 0, randomPitch);
                    var qb = Quaternion.FromEulerDegrees(randomYaw, i, randomPitch);
                    var angleDiff = qa.RotationFrom(qb).Decomposite().Angle * 180 / Math.PI;
                    // Print up to 5 decimal places
                    Console.WriteLine($"{i}°: {angleDiff:F5}°");
                }
            }
        }
    }
    public class Quaternion(double w, double x, double y, double z) : ICloneable {
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

        // A rotation in the order of 𝑥, 𝑦, 𝑧 axes is:
        // - If it's intrinsic rotation:
        // -- R = Rx * Ry' * Rz'' and Q = Qx * Qy * Qz
        // - If it's extrinsic rotation:
        // -- R = Rz * Ry * Rx and Q = Qz * Qy * Qx
        // Rx, Ry, Rz are rotation matrices representing rotation about x, y, z axes respectively.
        // Qx, Qy, Qz are quaternions representing rotation about x, y, z axes respectively.

        // ChatGPT says sometimes you need to reverse the order for intrinsic rotation, sometimes vice versa (inverse for extrinsic rotation). Don't trust it.

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

            // The rotation matrix corresponding to the quaternion q:
            //
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
                // Let's assume 𝜃z = 0, then 𝜃y = -a
                theta_z = 0;
                theta_y = -Math.Atan2(Rq[2, 0], Rq[0, 0]);
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
                // Let's assume 𝜃z = 0, then 𝜃y = a
                theta_z = 0;
                theta_y = Math.Atan2(-Rq[2, 0], Rq[0, 0]);
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
            /* Say there are two rotational matrices A, B and quaternions q, r representing those two respectively.
             * Say the product rotation C = AB.
             * How to calculate quaternion corresponding to C? */
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
            return $"({SCSDecimalParser.EncodeDecimal(w)}; {SCSDecimalParser.EncodeDecimal(x)}, {SCSDecimalParser.EncodeDecimal(y)}, {SCSDecimalParser.EncodeDecimal(z)})";
        }

        public string ToAxisAngleString() {
            var (axis, angle) = Decomposite();
            return $"{axis} {angle * 180 / Math.PI}";
        }

        public object Clone() {
            return new Quaternion(w, x, y, z);
        }
    }

    public class Vector3(double x, double y, double z) : ICloneable {
        public double x = x, y = y, z = z;

        public static readonly Vector3 UnitX = new(1, 0, 0);
        public static readonly Vector3 UnitY = new(0, 1, 0);
        public static readonly Vector3 UnitZ = new(0, 0, 1);

        public static readonly Vector3 UnitSCSYaw = UnitY;
        public static readonly Vector3 UnitSCSPitch = UnitX;
        public static readonly Vector3 UnitSCSRoll = UnitZ;

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

        // Scalar multiplication
        public static Vector3 operator *(Vector3 left, double right) {
            return new Vector3(left.x * right, left.y * right, left.z * right);
        }
        public static Vector3 operator *(double left, Vector3 right) {
            return new Vector3(left * right.x, left * right.y, left * right.z);
        }

        public Vector3 Normalize() {
            // ChatGPT says this is still recommended rather than Clone then Length = 1. because it doesn't add method calls nor calculate Length twice.
            double length = Length;
            if (length == 0) return new Vector3(0, 0, 0);
            return new Vector3(x / length, y / length, z / length);
        }

        // Gets or sets the length of the vector.
        public double Length {
            get {
                return Math.Sqrt(x * x + y * y + z * z);
            }
            set {
                double length = Length;
                if (length == 0) return;
                x *= value / length;
                y *= value / length;
                z *= value / length;
            }
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

        public Quaternion AsAxisAngleDegrees(double degrees) {
            return AsAxisAngle(degrees * Math.PI / 180);
        }

        public override string ToString() {
            return $"({SCSDecimalParser.EncodeDecimal(x)}, {SCSDecimalParser.EncodeDecimal(y)}, {SCSDecimalParser.EncodeDecimal(z)})";
        }

        public string ToHumanString() {
            return $"({x}, {y}, {z})";
        }

        public object Clone() {
            return new Vector3(x, y, z);
        }
    }

    public class SCSPlacement(Vector3 position, Quaternion orientation) : ICloneable {
        public Vector3 Position = position;
        public Quaternion Orientation = orientation;

        public static SCSPlacement Parse(string value) {
            ArgumentNullException.ThrowIfNull(value);

            // (x, y, z) (w; x, y, z)
            // So we will split the string into two parts and parse them separately in Vector3 and Quaternion.

            int firstSegEnd = value.IndexOf(')', StringComparison.InvariantCulture);
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

        public object Clone() {
            return new SCSPlacement((Vector3)Position.Clone(), (Quaternion)Orientation.Clone());
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

        public static string EncodeDecimal(double value, DecimalEncodingType? type = DecimalEncodingType.AutoSingle) {
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
                    return "&" + string.Join("", bytes.Select(b => b.ToString("x2")));
                case DecimalEncodingType.IEEE754Double:
                    bytes = ByteEncoder.EncodeDouble(value, ByteOrder.BigEndian);
                    return "&" + string.Join("", bytes.Select(b => b.ToString("x2")));
                default:
                    throw new ArgumentException("Invalid encoding type.");
            }
        }
    }
}