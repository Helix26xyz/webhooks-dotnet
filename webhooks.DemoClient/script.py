import sys
import requests
import json

# Define the URL
url = "https://ntfy.sh/UqPKuZovJiudiBzB"

# Read the input from stdin
input_data = sys.stdin.read()

try:
    data = json.loads(input_data)
except json.JSONDecodeError:
    data = input_data

# Get the command-line arguments
args = " ".join(sys.argv[1:])

# Define the payload
payload = {"text": f"PyHello, World! from {args}, {data}"}

# Send the POST request
response = requests.post(url, json=payload)

# Print the response status and content
print(f"Status Code: {response.status_code}")
print(f"Response: {response.text}")