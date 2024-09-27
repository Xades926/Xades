import LogData
import os


class JavaLogCollector:
    def __init__(self, data):
        self.data = data

    def collect_java_logs(self):
        log_path = os.path.join(self.data.apk_dir, 'output', 'FlowDroidLog1.txt')
        log_file = open(log_path, 'r')
        lines = log_file.readlines()

        cg_check = 0
        for line in lines:
            if line.startswith('\t\'Java to C/C++\' Edges Cnt'):
                self.data.ndk_edge_cnt = int(line.strip().split()[-1])
            if line.startswith('\t\'Java to C/C++\' Library Cnt'):
                self.data.ndk_lib_cnt = int(line.strip().split()[-1])

        log_path = os.path.join(self.data.apk_dir, 'output', 'FlowDroidLog2.txt')
        log_file = open(log_path, 'r', encoding='utf-8', errors='ignore')
        lines = log_file.readlines()
        running_time = 0
        for line in lines:
            if 'seconds' in line:
                splits = line.replace('\n','').split(' ')

                for idx in range(len(splits)):
                    if splits[idx] == 'seconds':
                        self.data.java_time += float(splits[idx-1])
                        break


            if line.startswith(
                    '[main] INFO soot.jimple.infoflow.android.SetupApplication$InPlaceInfoflow - Callgraph has') and cg_check == 0:
                self.data.fd_cg_edge1 = int(line.split(' ')[-2].split()[-1])
                cg_check += 1
            elif line.startswith(
                    '[main] INFO soot.jimple.infoflow.android.SetupApplication$InPlaceInfoflow - Callgraph has') and cg_check == 1:
                self.data.fd_cg_edge2 = int(line.split(' ')[-2].split()[-1])
                cg_check += 1

            if line.startswith(
                    '[main] INFO soot.jimple.infoflow.android.SetupApplication$InPlaceInfoflow - IFDS problem with'):
                self.data.fd_time = int(line.split(' ')[-5].split()[-1])

            if line.startswith('[main] INFO soot.jimple.infoflow.android.SetupApplication$InPlaceInfoflow - Data flow solver took 0 seconds. Maximum memory consumption:'):
                self.data.java_memoryUsage = float(line.split(' ')[-2].split()[-1])
            if line.endswith('leaks\n'):
                self.data.total_leaks = int(line.split(' ')[-2].split()[-1])

        if cg_check < 2:
            self.data.java_error = "timeout in FlowDroid"
        return self.data
