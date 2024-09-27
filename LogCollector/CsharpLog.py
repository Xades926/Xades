import os


class CsharpLogCollector:
    def __init__(self, data):
        self.data = data

    def generate_error(self):
        err_path = os.path.join(self.data.apk_dir, 'output', 'dotnetError.txt')
        err_size = os.path.getsize(err_path)

        err_file = open(err_path, 'r')
        if int(self.data.dll_cnt) == 0:
            error = 'dll not found'
        elif err_size == 0:
            error = ''
        else:
            error = err_file.read(err_size).split()


        if self.data.cg_node == 0:
            error = 'csharp error'
        return error

    def collect_csharp_logs(self):
        log_path = os.path.join(self.data.apk_dir, 'output', 'dotnetLog.txt')
        log_file = open(log_path, 'r', encoding='utf-8', errors='ignore')
        lines = log_file.readlines()

        for line in lines:
            if line.startswith('Loading project'):
                self.data.dll_cnt += 1

            if line.startswith('\tCsharp call graph # nodes ='):
                self.data.cg_node = int(line.split(' ')[-1].split()[-1])
            if line.startswith('\tCsharp call graph # edges ='):
                self.data.cg_edge = int(line.split(' ')[-1].split()[-1])

            if line.startswith('\t\'Csharp to C/C++\' nodes '):
                self.data.cpp_node = int(line.strip().split()[-1])
            if line.startswith('\t\'Csharp to C/C++\' edges '):
                self.data.cpp_edge = int(line.strip().split()[-1])
            if line.startswith('\t\'Csharp to C/C++\' Custom'):
                self.data.cpp_custom_edges = int(line.strip().split()[-1])

            if line.startswith('\t\'Csharp to Java\' nodes '):
                self.data.java_node = int(line.strip().split()[-1])
            if line.startswith('\t\'Csharp to Java\' edges '):
                self.data.java_edge = int(line.strip().split()[-1])
            if line.startswith('\t\'Csharp to Java\' Custom'):
                self.data.java_custom_edges = int(line.strip().split()[-1])

            if line.startswith('\tTainted nodeCnt'):
                self.data.tainted_node = int(line.strip().split()[-1])
            if line.startswith('\tTainted edgeCnt'):
                self.data.tainted_edge = int(line.strip().split()[-1])

            # if line.startswith('  Taint-Analysis Result: Found Sources to Csharp'):
            #     self.data.CsharpSource_cnt = int(line.split(' ')[-1].split()[-1])
            # if line.startswith('  Taint-Analysis Result: Found Sinks to Csharp'):
            #     self.data.CsharpSink_cnt = int(line.split(' ')[-1].split()[-1])
            # if line.startswith('  Taint-Analysis Result: Found Boths to Csharp'):
            #     self.data.CsharpBoth_cnt = int(line.split(' ')[-1].split()[-1])
            if line.startswith('  Taint-Analysis Result: Found Leaks to Csharp'):
                self.data.CsharpLeak_cnt = int(line.split(' ')[-1].split()[-1])
            #
            # if line.startswith('  Taint-Analysis Result: Found Sources to Java'):
            #     self.data.JavaSource_cnt = int(line.split(' ')[-1].split()[-1])
            # if line.startswith('  Taint-Analysis Result: Found Sinks to Java'):
            #     self.data.JavaSink_cnt = int(line.split(' ')[-1].split()[-1])
            # if line.startswith('  Taint-Analysis Result: Found Boths to Java'):
            #     self.data.JavaBoth_cnt = int(line.split(' ')[-1].split()[-1])
            if line.startswith('  Taint-Analysis Result: Found Leaks to Java'):
                self.data.JavaLeak_cnt = int(line.split(' ')[-1].split()[-1])
            #
            # if line.startswith('  Taint-Analysis Result: Found Sources to Cpp'):
            #     self.data.CppSource_cnt = int(line.split(' ')[-1].split()[-1])
            # if line.startswith('  Taint-Analysis Result: Found Sinks to Cpp'):
            #     self.data.CppSink_cnt = int(line.split(' ')[-1].split()[-1])
            # if line.startswith('  Taint-Analysis Result: Found Boths to Cpp'):
            #     self.data.CppBoth_cnt = int(line.split(' ')[-1].split()[-1])
            if line.startswith('  Taint-Analysis Result: Found Leaks to Cpp'):
                self.data.CppLeak_cnt = int(line.split(' ')[-1].split()[-1])

            if line.startswith('Running Time:'):
                self.data.runningTime = float(line.split(' ')[2])
            if line.startswith('MemoryUsage:'):
                self.data.memoryUsage = float(line.split(' ')[1])

        self.check_jimple()
        self.data.error = self.generate_error()

        return self.data

    def check_jimple(self):
        file_path = os.path.join(self.data.apk_dir, 'output/csharp2jimple.json')
        check = False

        if os.path.exists(file_path):
            with open(file_path, 'r', encoding='utf-8', errors='ignore') as fp:
                if len(fp.read()) > 2:
                    check = True

        if check:
            self.data.convert_jimple = 'O'