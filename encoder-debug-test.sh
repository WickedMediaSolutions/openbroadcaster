#!/bin/bash
# Encoder Debug Test Script
# This script will show you all encoder debug output in real-time

set -e

echo "====================================="
echo "OpenBroadcaster Encoder Debug Monitor"
echo "====================================="
echo ""
echo "Monitoring log files for encoder debug output..."
echo ""

# Function to show logs with ENCODER debug messages
monitor_logs() {
    while true; do
        # Check main app log
        if [ -f ~/.local/share/OpenBroadcaster/logs/*.log ]; then
            tail -f ~/.local/share/OpenBroadcaster/logs/*.log 2>/dev/null | grep -E "\[ENCODER|ERROR|FAILED|error" &
            LOG_PID=$!
        fi
        
        # Also show the /tmp log if app is running
        if [ -f /tmp/encoder-debug.log ]; then
            tail -f /tmp/encoder-debug.log 2>/dev/null | grep -E "\[ENCODER|ERROR|FAILED|error" &
            LOG_PID2=$!
        fi
        
        # Show stdout from running process
        ps aux | grep "OpenBroadcaster" | grep -v grep >/dev/null && {
            echo ""
            echo "[DEBUG] App is running - open it and try connecting an encoder"
            echo "[DEBUG] Watch this window for debug output..."
            echo ""
        }
        
        wait $LOG_PID $LOG_PID2 2>/dev/null
        sleep 1
    done
}

# Show what to do
echo "ACTION REQUIRED:"
echo "1. Open the OpenBroadcaster application window"
echo "2. Go to Settings → Encoders"
echo "3. Configure an encoder profile (or use existing one)"
echo "4. Try to start/test the encoder"
echo "5. Watch THIS TERMINAL for debug output"
echo ""
echo "====================================="
echo "Debug output:"
echo "====================================="
echo ""

# Just tail the logs
tail -f ~/.local/share/OpenBroadcaster/logs/*.log 2>/dev/null | grep -E "\[ENCODER|TCP|SSL|PROTOCOL|ERROR|FAILED|error" || echo "Waiting for logs..."
