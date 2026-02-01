#!/bin/bash
# Quick encoder diagnostic test script
# This creates a test Icecast mount and tests encoder connection

set -e

HOST="${1:-localhost}"
PORT="${2:-8000}"
MOUNT="${3:-/test}"
USER="${4:-source}"
PASS="${5:-hackme}"

echo "========================================="
echo "  OpenBroadcaster Encoder Test"
echo "========================================="
echo ""
echo "Target: $HOST:$PORT$MOUNT"
echo "Credentials: $USER:$PASS"
echo ""

# Test TCP connection
echo "[1/3] Testing TCP connection to $HOST:$PORT..."
if nc -z -w 2 "$HOST" "$PORT" 2>/dev/null; then
    echo "✓ TCP connection successful"
else
    echo "✗ TCP connection FAILED - Icecast server not responding"
    echo "  Make sure Icecast is running on $HOST:$PORT"
    exit 1
fi
echo ""

# Test Icecast handshake
echo "[2/3] Testing Icecast SOURCE handshake..."
CREDS=$(echo -n "$USER:$PASS" | base64)
REQUEST="SOURCE $MOUNT HTTP/1.1\r\nHost: $HOST:$PORT\r\nAuthorization: Basic $CREDS\r\nContent-Type: audio/mpeg\r\n\r\n"
RESPONSE=$(printf "$REQUEST" | nc "$HOST" "$PORT" 2>/dev/null | head -1)
if echo "$RESPONSE" | grep -q "200"; then
    echo "✓ Icecast handshake successful"
    echo "  Response: $RESPONSE"
else
    echo "✗ Icecast handshake FAILED"
    echo "  Response: $RESPONSE"
    if echo "$RESPONSE" | grep -q "403"; then
        echo "  Error: Mount point rejected (403 Forbidden)"
        echo "  Check: Is the mount point configured? Does source have permission?"
    elif echo "$RESPONSE" | grep -q "404"; then
        echo "  Error: Mount point not found (404)"
        echo "  Check: Is Icecast configured with the '$MOUNT' mount point?"
    fi
    exit 1
fi
echo ""

# Test admin credentials (optional)
echo "[3/3] Testing Icecast admin interface..."
ADMIN_USER="${6:-admin}"
ADMIN_PASS="${7:-hackme}"
ADMIN_CREDS=$(echo -n "$ADMIN_USER:$ADMIN_PASS" | base64)
ADMIN_RESPONSE=$(curl -s -H "Authorization: Basic $ADMIN_CREDS" "http://$HOST:$PORT/admin/stats.xml" | head -1)
if echo "$ADMIN_RESPONSE" | grep -q "icestats"; then
    echo "✓ Admin interface accessible"
else
    echo "ℹ Admin interface not accessible (this is optional)"
    echo "  Admin credentials: $ADMIN_USER"
fi
echo ""

echo "========================================="
echo "  All tests passed! ✓"
echo "========================================="
echo ""
echo "Your encoder can now connect with these settings:"
echo "  Host: $HOST"
echo "  Port: $PORT"
echo "  Mount: $MOUNT"
echo "  Username: $USER"
echo "  Password: $PASS"
echo "  Protocol: Icecast"
