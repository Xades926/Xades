import os.path
import sys
import csv
import subprocess

folder_path = "/Users/sehwan/WorkDir/evaluation/2022_1/"
if __name__ == '__main__':
    subfolder_paths = []
    for root, dirs, files in os.walk(folder_path):
        for directory in dirs:
            subfolder_paths.append(os.path.join(root, directory))

    for path in subfolder_paths:
        file_path = os.path.join(path, 'output/csharp2jimple.json')
        check = False

        if os.path.exists(file_path):
            with open(file_path, 'r', encoding='utf-8', errors='ignore') as fp:
                if len(fp.read()) > 2:
                    check = True

        if check:
            print(file_path)


