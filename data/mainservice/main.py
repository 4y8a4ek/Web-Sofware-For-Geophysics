import os
import uuid
import json
import asyncio
from fastapi import FastAPI, UploadFile, File, Form, HTTPException, BackgroundTasks
from fastapi.responses import FileResponse, HTMLResponse
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates
from starlette.requests import Request
import numpy as np
from skimage.measure import marching_cubes
import open3d as o3d
from config import DATA_DIR
from aiokafka import AIOKafkaProducer
import logging

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(title="Mesh Generator MVP")

app.mount("/static", StaticFiles(directory="static"), name="static")
templates = Jinja2Templates(directory="templates")

producer = None
KAFKA_BOOTSTRAP_SERVERS = os.getenv("KAFKA_BOOTSTRAP_SERVERS", "kafka:9092")

async def init_kafka():
    global producer
    while True:
        try:
            producer = AIOKafkaProducer(
                bootstrap_servers=KAFKA_BOOTSTRAP_SERVERS,
                value_serializer=lambda v: json.dumps(v).encode('utf-8')
            )
            await producer.start()
            logger.info("✅ Kafka producer started")
            break
        except Exception as e:
            logger.warning(f"⚠️ Kafka not ready: {e}, retrying in 5s...")
            await asyncio.sleep(5)

@app.on_event("startup")
async def startup():
    asyncio.create_task(init_kafka())

@app.on_event("shutdown")
async def shutdown():
    if producer:
        await producer.stop()

tasks = {}  # in-memory

def generate_stl(raw_path: str, shape, factor: int) -> str:
    data = np.fromfile(raw_path, dtype=np.uint8).reshape(shape)
    data = 1 - data
    if factor > 1:
        data = data[::factor, ::factor, ::factor]
    verts, faces, _, _ = marching_cubes(data, level=0.5, spacing=(1.0, 1.0, 1.0))
    mesh = o3d.geometry.TriangleMesh()
    mesh.vertices = o3d.utility.Vector3dVector(verts)
    mesh.triangles = o3d.utility.Vector3iVector(faces)
    mesh.compute_vertex_normals()
    stl_path = os.path.join(DATA_DIR, f"{uuid.uuid4()}.stl")
    o3d.io.write_triangle_mesh(stl_path, mesh)
    return stl_path

async def process_task(task_id: str, raw_path: str, shape, factor: int):
    try:
        tasks[task_id]["status"] = "processing"
        stl_path = generate_stl(raw_path, shape, factor)
        tasks[task_id]["status"] = "done"
        tasks[task_id]["stl_path"] = stl_path

        # Отправляем задачу в Kafka
        task_data = {
            "task_id": task_id,
            "stl_path": stl_path,
            "shape": shape,
            "factor": factor
        }
        # Ждём пока producer инициализируется
        while producer is None:
            await asyncio.sleep(0.5)
        await producer.send("tasks", value=task_data)

    except Exception as e:
        tasks[task_id]["status"] = "error"
        tasks[task_id]["error"] = str(e)
    finally:
        if os.path.exists(raw_path):
            os.remove(raw_path)

@app.post("/upload")
async def upload_file(
    background_tasks: BackgroundTasks,
    file: UploadFile = File(...),
    shape_x: int = Form(200),
    shape_y: int = Form(200),
    shape_z: int = Form(200),
    factor: int = Form(1)
):
    if not file.filename.endswith('.raw'):
        raise HTTPException(400, "Only .raw files are supported")
    if factor < 1:
        raise HTTPException(400, "Factor must be >= 1")
    task_id = str(uuid.uuid4())
    raw_path = os.path.join(DATA_DIR, f"{task_id}.raw")
    with open(raw_path, "wb") as f:
        content = await file.read()
        f.write(content)
    tasks[task_id] = {
        "status": "pending",
        "shape": (shape_x, shape_y, shape_z),
        "factor": factor,
        "raw_path": raw_path
    }
    background_tasks.add_task(process_task, task_id, raw_path, (shape_x, shape_y, shape_z), factor)
    return {"task_id": task_id, "status": "pending"}

@app.get("/status/{task_id}")
async def get_status(task_id: str):
    if task_id not in tasks:
        raise HTTPException(404, "Task not found")
    return tasks[task_id]

@app.get("/download/{task_id}")
async def download_stl(task_id: str, background_tasks: BackgroundTasks):
    task = tasks.get(task_id)
    if not task or task["status"] != "done":
        raise HTTPException(404, "STL not ready or task not found")
    stl_path = task.get("stl_path")
    if not stl_path or not os.path.exists(stl_path):
        raise HTTPException(404, "STL file missing")
    def delete_file(path):
        if os.path.exists(path):
            os.remove(path)
    background_tasks.add_task(delete_file, stl_path)
    return FileResponse(
        stl_path,
        media_type="application/vnd.ms-pki.stl",
        filename=f"model_{task_id}.stl"
    )

@app.get("/", response_class=HTMLResponse)
async def index(request: Request):
    return templates.TemplateResponse("index.html", {"request": request})