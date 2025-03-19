#!/usr/bin/env python3
import os
import time
import threading
import re
import datetime
from functools import wraps
from flask import request, Flask, jsonify


API_PASSPHRASE = "mySecretPassphrase"  # Change this to your desired passphrase

def require_passphrase(f):
    @wraps(f)
    def decorated_function(*args, **kwargs):
        # Look for the passphrase in the query string or in the 'X-Passphrase' header.
        passphrase = request.args.get('passphrase') or request.headers.get('X-Passphrase')
        if passphrase != API_PASSPHRASE:
            return jsonify({'error': 'Unauthorized access'}), 403
        return f(*args, **kwargs)
    return decorated_function

app = Flask(__name__)

LOG_FILE = '/opt/ghidra-repos/server.log'
# Global dictionaries to track connected users and repository events.
# For connected_users, we count each handle generation so that if a user connects twice, they’re tracked correctly.
connected_users = {}  # {username: count}
repo_events = []      # List of events (each event is a dict with 'timestamp' and 'message')

def remove_ip(s):
    """
    Remove IP addresses from a given string.
    It strips the '@' and following digits/dots.
    """
    return re.sub(r'@[\d\.]+', '', s)

def process_line(line):
    """
    Parse a single log line to update connected users and repository events.
    """
    if len(line) < 20:
        return
    # Extract timestamp from the first 19 characters.
    timestamp_str = line[:19]
    try:
        timestamp = datetime.datetime.strptime(timestamp_str, '%Y-%m-%d %H:%M:%S')
    except ValueError:
        return

    # Get module name from text between the first pair of parentheses.
    module_match = re.search(r'\((.*?)\)', line)
    module = module_match.group(1) if module_match else ""
    
    # Extract the message content (everything after the module parentheses)
    end_paren = line.find(') ')
    message = line[end_paren+2:].strip() if end_paren != -1 else line.strip()
    clean_message = remove_ip(message)  # Hide IP addresses in the stored message

    # If the line is from RepositoryManager, store it as an event.
    if module == "RepositoryManager":
        # Only store events that are not "Repository server handle requested" or "User '...' authenticated"
        if not (re.match(r"^Repository server handle requested \(\S+\)$", clean_message) or
                re.match(r"^User '.+' authenticated \(\S+\)$", clean_message)):
            repo_events.append({'timestamp': timestamp, 'message': clean_message})
        
        # Update connected users based on handle events.
        if "generated handle" in message:
            m = re.search(r'\((\S+)@', message)
            if m:
                username = m.group(1)
                connected_users[username] = connected_users.get(username, 0) + 1
        elif "handle disposed" in message:
            m = re.search(r'\((\S+?)(?:@|\))', message)
            if m:
                username = m.group(1)
                if username in connected_users:
                    connected_users[username] -= 1
                    if connected_users[username] <= 0:
                        del connected_users[username]


def initial_load():
    """
    Process the entire existing log file for initial state.
    """
    if not os.path.exists(LOG_FILE):
        return
    with open(LOG_FILE, 'r') as f:
        for line in f:
            process_line(line)

def tail_log_file():
    """
    Tail the log file to process new entries as they are written.
    """
    with open(LOG_FILE, 'r') as f:
        # Go to the end of the file so we don't re-read old lines.
        f.seek(0, os.SEEK_END)
        while True:
            line = f.readline()
            if not line:
                time.sleep(1)
                continue
            process_line(line)

def prune_events():
    """
    Periodically remove events older than 2 minutes to avoid memory build-up.
    """
    while True:
        now = datetime.datetime.now()
        # Keep only events from the last 2 minutes.
        repo_events[:] = [e for e in repo_events if (now - e['timestamp']).total_seconds() <= 120]
        time.sleep(60)

@app.route('/users')
@require_passphrase
def get_users():
    """
    API endpoint to return current connected users.
    The usernames will not show any IP addresses.
    """
    # Return only the username keys.
    users = list(connected_users.keys())
    return jsonify({'users': users})

@app.route('/events')
@require_passphrase
def get_events():
    """
    API endpoint to return RepositoryManager events from the last minute.
    """
    now = datetime.datetime.now()
    recent = []
    for event in repo_events:
        if (now - event['timestamp']).total_seconds() <= 60:
            # Format the timestamp as a string.
            recent.append({
                'timestamp': event['timestamp'].strftime('%Y-%m-%d %H:%M:%S'),
                'message': event['message']
            })
    return jsonify({'events': recent})

def start_tail_thread():
    """
    Start a background thread to tail the log file.
    """
    t = threading.Thread(target=tail_log_file, daemon=True)
    t.start()

def start_prune_thread():
    """
    Start a background thread to prune old events.
    """
    t = threading.Thread(target=prune_events, daemon=True)
    t.start()

if __name__ == '__main__':
    # Check for sudo access. If not run as root, exit.
    if os.geteuid() != 0:
        print("This program requires sudo access to access the log file.")
        exit(1)
    
    # Load initial state from the log file.
    initial_load()
    
    # Start background threads for tailing the log and pruning old events.
    start_tail_thread()
    start_prune_thread()
    
    # Start the Flask API server.
    app.run(host='0.0.0.0', port=5000)
