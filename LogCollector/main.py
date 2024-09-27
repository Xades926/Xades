import os.path
import sys

from CsharpLog import CsharpLogCollector
from JavaLog import JavaLogCollector
from LogData import LogData

if __name__ == '__main__':
    if len(sys.argv) != 3:
        sys.exit(" usage: ./command <apk_directory>  <out_path>")

    data = LogData()
    data.apk_dir = sys.argv[1]
    data.apk_name = os.path.split(data.apk_dir)[1]

    output_path = sys.argv[2]


    csharpLogs = CsharpLogCollector(data)
    data = csharpLogs.collect_csharp_logs()

    javaLogs = JavaLogCollector(data)
    data = javaLogs.collect_java_logs()

    data.write_csv(output_path)
