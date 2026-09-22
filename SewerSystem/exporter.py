import json
import sys
import openpyxl

json_file = sys.argv[1]

with open(json_file, 'r') as f:
    data = json.load(f)

wb = openpyxl.Workbook()
wb = wb.active
ws.title = "Data"

ws.append(["Column 1", "Column 2", "Column 3"])  # Add headers
for item in data:
    ws.append([item.get("column1"), item.get("column2"), item.get("column3")])  # Add data rows

wb.save("output.xlsx")