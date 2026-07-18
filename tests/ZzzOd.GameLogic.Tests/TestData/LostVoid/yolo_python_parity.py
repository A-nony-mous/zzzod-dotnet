import json
import shutil
import sys
import tempfile
from pathlib import Path

import cv2

from one_dragon.yolo.yolov8_onnx_det import Yolov8Detector


def main() -> None:
    model_dir = Path(sys.argv[1]).resolve()
    image_path = Path(sys.argv[2]).resolve()
    output_path = Path(sys.argv[3]).resolve()
    model_name = model_dir.name

    with tempfile.TemporaryDirectory(prefix="zzzod-yolo-python-parity-") as temp_dir:
        temporary_model_dir = Path(temp_dir) / model_name
        temporary_model_dir.mkdir(parents=True)
        shutil.copy2(model_dir / "model.onnx", temporary_model_dir / "model.onnx")
        labels = (model_dir / "model_label.txt").read_text(encoding="utf-8").splitlines()
        (temporary_model_dir / "labels.csv").write_text(
            "idx,class_name,category\n" + "".join(f"{index},{label},\n" for index, label in enumerate(labels)),
            encoding="utf-8",
        )

        detector = Yolov8Detector(
            model_name=model_name,
            model_parent_dir_path=temp_dir,
            model_download_url="",
            gh_proxy=False,
        )
        image = cv2.imread(str(image_path))
        if image is None:
            raise RuntimeError(f"无法读取固定截图 {image_path}")

        frame = detector.run(image, conf=0.6, iou=0.5, run_time=0.0)
        results = [
            {
                "className": result.detect_class.class_name,
                "score": result.score,
                "x1": result.x1,
                "y1": result.y1,
                "x2": result.x2,
                "y2": result.y2,
            }
            for result in frame.results
        ]

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(results, ensure_ascii=False), encoding="utf-8")


if __name__ == "__main__":
    main()
