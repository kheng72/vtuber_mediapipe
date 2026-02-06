import cv2
import mediapipe as mp
import numpy as np

mp_pose = mp.solutions.pose
mp_drawing = mp.solutions.drawing_utils

pose = mp_pose.Pose(
    static_image_mode=False,
    model_complexity=1,
    smooth_landmarks=True,
    min_detection_confidence=0.5,
    min_tracking_confidence=0.5
)

cap = cv2.VideoCapture(0)

while cap.isOpened():
    success, frame = cap.read()
    if not success:
        break

    image = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    image.flags.writeable = False

    results = pose.process(image)

    image.flags.writeable = True
    image = cv2.cvtColor(image, cv2.COLOR_RGB2BGR)

    pose_array = None

    if results.pose_landmarks:
        pose_array = np.array([
            [lm.x, lm.y, lm.z, lm.visibility]
            for lm in results.pose_landmarks.landmark
        ])

        mp_drawing.draw_landmarks(
            image,
            results.pose_landmarks,
            mp_pose.POSE_CONNECTIONS
        )

        # debug
        print("Pose shape:", pose_array.shape)
        print("LEFT_WRIST:", pose_array[mp_pose.PoseLandmark.LEFT_WRIST])

    # 🔽 ลดขนาดภาพเหลือครึ่งหนึ่ง
    
    image_half = cv2.resize(
        image,
        (800, 720),
        interpolation=cv2.INTER_AREA
    )

    cv2.imshow("MediaPipe Pose", image_half)

    if cv2.waitKey(1) & 0xFF == 27:
        break

cap.release()
cv2.destroyAllWindows()
