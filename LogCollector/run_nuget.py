import os.path
import sys
import csv
import subprocess

if __name__ == '__main__':
    if len(sys.argv) != 2:
        sys.exit(" usage: ./command <logfolder_path>")

    logfolder_path = sys.argv[1]

    subfolder_paths = []
    for item in os.listdir(logfolder_path):
        item_path = os.path.join(logfolder_path, item)
        if os.path.isdir(item_path):
            subfolder_paths.append(os.path.abspath(item_path))

    for item in subfolder_paths:
        print(item)
        script_path = f'main.py'
        params = [item, './result_nuget.csv']
        subprocess.run(["python3", script_path] + params)