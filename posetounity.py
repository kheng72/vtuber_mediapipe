import cv2
import mediapipe as mp
import socket
import json

# =======================
# MediaPipe Pose
# =======================
mp_pose = mp.solutions.pose

# =======================
# UDP Socket (ไป Unity)
# =======================
UDP_IP = "127.0.0.1"   # ถ้า Unity อยู่เครื่องเดียวกัน
UDP_PORT = 5055
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

# เลือก landmark ที่จะส่ง
LANDMARKS = {
    "LEFT_SHOULDER": mp_pose.PoseLandmark.LEFT_SHOULDER,
    "RIGHT_SHOULDER": mp_pose.PoseLandmark.RIGHT_SHOULDER,
    "LEFT_ELBOW": mp_pose.PoseLandmark.LEFT_ELBOW,
    "RIGHT_ELBOW": mp_pose.PoseLandmark.RIGHT_ELBOW,
    "LEFT_WRIST": mp_pose.PoseLandmark.LEFT_WRIST,
    "RIGHT_WRIST": mp_pose.PoseLandmark.RIGHT_WRIST,
    "LEFT_HIP": mp_pose.PoseLandmark.LEFT_HIP,
    "RIGHT_HIP": mp_pose.PoseLandmark.RIGHT_HIP
}

cap = cv2.VideoCapture(0)

with mp_pose.Pose(
    static_image_mode=False,
    model_complexity=1,
    smooth_landmarks=True,
    min_detection_confidence=0.5,
    min_tracking_confidence=0.5
) as pose:

    while cap.isOpened():
        success, frame = cap.read()
        if not success:
            break

        image_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        results = pose.process(image_rgb)

        if results.pose_landmarks:
            data = {}

            for name, lm_id in LANDMARKS.items():
                lm = results.pose_landmarks.landmark[lm_id]
                data[name] = [lm.x, lm.y]  # normalized 0-1

            json_data = json.dumps(data)
            sock.sendto(json_data.encode("utf-8"), (UDP_IP, UDP_PORT))

            # debug ดูใน terminal
            print(json_data)

        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

cap.release()
