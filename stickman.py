import cv2
import mediapipe as mp
import numpy as np

# ตั้งค่า MediaPipe Pose
mp_pose = mp.solutions.pose
mp_drawing = mp.solutions.drawing_utils

def draw_stick_figure(image, landmarks):
    h, w, c = image.shape
    def to_pixel(landmark):
        return (int(landmark.x * w), int(landmark.y * h))
    
    # ดึงค่าจุดเชื่อมต่อที่สำคัญ (จาก example3.py)
    left_shoulder = landmarks[mp_pose.PoseLandmark.LEFT_SHOULDER]
    right_shoulder = landmarks[mp_pose.PoseLandmark.RIGHT_SHOULDER]
    left_elbow = landmarks[mp_pose.PoseLandmark.LEFT_ELBOW]
    right_elbow = landmarks[mp_pose.PoseLandmark.RIGHT_ELBOW]
    left_wrist = landmarks[mp_pose.PoseLandmark.LEFT_WRIST]
    right_wrist = landmarks[mp_pose.PoseLandmark.RIGHT_WRIST]
    left_hip = landmarks[mp_pose.PoseLandmark.LEFT_HIP]
    right_hip = landmarks[mp_pose.PoseLandmark.RIGHT_HIP]
    left_knee = landmarks[mp_pose.PoseLandmark.LEFT_KNEE]
    right_knee = landmarks[mp_pose.PoseLandmark.RIGHT_KNEE]
    left_ankle = landmarks[mp_pose.PoseLandmark.LEFT_ANKLE]
    right_ankle = landmarks[mp_pose.PoseLandmark.RIGHT_ANKLE]

    color = (0, 100, 255) 
    thickness = 15

    shoulder_width = abs(left_shoulder.x - right_shoulder.x) * w
    head_radius = int(shoulder_width * 0.3)
    neck_x, neck_y = (left_shoulder.x + right_shoulder.x) / 2, (left_shoulder.y + right_shoulder.y) / 2
    hip_x, hip_y = (left_hip.x + right_hip.x) / 2, (left_hip.y + right_hip.y) / 2

    neck_pixel = (int(neck_x * w), int(neck_y * h))
    hip_pixel = (int(hip_x * w), int(hip_y * h))
    head_pixel = (int(neck_x * w), int((neck_y * h) - head_radius - 10))

    cv2.line(image, neck_pixel, hip_pixel, color, thickness)
    cv2.line(image, neck_pixel, to_pixel(left_elbow), color, thickness)
    cv2.line(image, to_pixel(left_elbow), to_pixel(left_wrist), color, thickness)
    cv2.line(image, neck_pixel, to_pixel(right_elbow), color, thickness)
    cv2.line(image, to_pixel(right_elbow), to_pixel(right_wrist), color, thickness)
    cv2.line(image, hip_pixel, to_pixel(left_knee), color, thickness)
    cv2.line(image, to_pixel(left_knee), to_pixel(left_ankle), color, thickness)
    cv2.line(image, hip_pixel, to_pixel(right_knee), color, thickness)
    cv2.line(image, to_pixel(right_knee), to_pixel(right_ankle), color, thickness)
    cv2.circle(image, head_pixel, head_radius, color, -1)

cap = cv2.VideoCapture(0)

# กำหนดค่าพารามิเตอร์ MediaPipe (จาก main.py)
with mp_pose.Pose(
    static_image_mode=False,
    model_complexity=1,
    smooth_landmarks=True,
    min_detection_confidence=0.5,
    min_tracking_confidence=0.5
) as pose:

    while cap.isOpened():
        success, frame = cap.read()
        if not success: break

        h, w, _ = frame.shape
        white_canvas = np.ones((h, w, 3), dtype=np.uint8) * 255
        image_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        results = pose.process(image_rgb)
        
        if results.pose_landmarks:
            # --- ส่วนการสร้าง Array และ Print ลง Terminal (จาก main.py) ---
            pose_array = np.array([
                [lm.x, lm.y, lm.z, lm.visibility]
                for lm in results.pose_landmarks.landmark
            ])
            
            # แสดงค่าใน Terminal แบบ Real-time
            print("-" * 30)
            print(f"Pose Shape: {pose_array.shape}") # จะได้ (33, 4) คือ 33 จุด x (x, y, z, visibility)
            print("Current Pose Array (First 5 landmarks):\n", pose_array[:5]) 
            # ---------------------------------------------------------

            # วาดเส้นปกติลงบนภาพจริง
            mp_drawing.draw_landmarks(frame, results.pose_landmarks, mp_pose.POSE_CONNECTIONS)
            # วาด Stick Figure ลงบนกระดาษขาว
            draw_stick_figure(white_canvas, results.pose_landmarks.landmark)

        # ปรับขนาดเพื่อแสดงผล
        cv2.imshow("Real Video", cv2.resize(frame, (640, 480)))
        cv2.imshow("Stick Figure", cv2.resize(white_canvas, (640, 480)))

        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

cap.release()
cv2.destroyAllWindows()