#!/usr/bin/env python3
"""
Kafka Consumer Demo Script

This script consumes messages from a Kafka topic to validate Kafka functionality.
"""

import json
import sys
from datetime import datetime
from kafka import KafkaConsumer
import argparse
import logging
import signal

# Configure logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)


class KafkaConsumerDemo:
    def __init__(self, bootstrap_servers, topic_name, group_id="demo-consumer-group"):
        self.bootstrap_servers = bootstrap_servers
        self.topic_name = topic_name
        self.group_id = group_id
        self.consumer = None
        self.running = True
        
    def connect(self, auto_offset_reset='latest'):
        """Initialize Kafka consumer connection"""
        try:
            self.consumer = KafkaConsumer(
                self.topic_name,
                bootstrap_servers=self.bootstrap_servers,
                group_id=self.group_id,
                value_deserializer=lambda x: json.loads(x.decode('utf-8')),
                key_deserializer=lambda x: x.decode('utf-8') if x else None,
                auto_offset_reset=auto_offset_reset,
                enable_auto_commit=True,
                auto_commit_interval_ms=1000,
                session_timeout_ms=30000,
                heartbeat_interval_ms=10000
            )
            logger.info(f"Connected to Kafka at {self.bootstrap_servers}")
            logger.info(f"Subscribed to topic '{self.topic_name}' with group ID '{self.group_id}'")
            return True
        except Exception as e:
            logger.error(f"Failed to connect to Kafka: {e}")
            return False
    
    def consume_messages(self, max_messages=None, timeout_seconds=None):
        """Consume messages from the Kafka topic"""
        if not self.consumer:
            logger.error("Consumer not initialized. Call connect() first.")
            return False
        
        messages_consumed = 0
        start_time = datetime.now()
        
        logger.info("Starting message consumption...")
        if max_messages:
            logger.info(f"Will consume up to {max_messages} messages")
        if timeout_seconds:
            logger.info(f"Will timeout after {timeout_seconds} seconds")
        
        try:
            # Set up signal handler for graceful shutdown
            def signal_handler(signum, frame):
                logger.info("Received interrupt signal. Shutting down gracefully...")
                self.running = False
            
            signal.signal(signal.SIGINT, signal_handler)
            signal.signal(signal.SIGTERM, signal_handler)
            
            # Consume messages
            for message in self.consumer:
                if not self.running:
                    break
                
                # Check timeout
                if timeout_seconds:
                    elapsed = (datetime.now() - start_time).total_seconds()
                    if elapsed > timeout_seconds:
                        logger.info(f"Timeout reached after {elapsed:.1f} seconds")
                        break
                
                # Process message
                self.process_message(message)
                messages_consumed += 1
                
                # Check max messages limit
                if max_messages and messages_consumed >= max_messages:
                    logger.info(f"Consumed {messages_consumed} messages (limit reached)")
                    break
                    
        except KeyboardInterrupt:
            logger.info("Consumer interrupted by user")
        except Exception as e:
            logger.error(f"Error during message consumption: {e}")
            return False
        finally:
            logger.info(f"Total messages consumed: {messages_consumed}")
        
        return True
    
    def process_message(self, message):
        """Process a single Kafka message"""
        try:
            # Extract message metadata
            partition = message.partition
            offset = message.offset
            key = message.key
            value = message.value
            timestamp = message.timestamp
            
            # Convert timestamp to readable format
            if timestamp:
                msg_time = datetime.fromtimestamp(timestamp / 1000).isoformat()
            else:
                msg_time = "Unknown"
            
            # Log message details
            logger.info("Received message:")
            logger.info(f"  Partition: {partition}, Offset: {offset}")
            logger.info(f"  Key: {key}")
            logger.info(f"  Timestamp: {msg_time}")
            
            # Pretty print the message content
            if isinstance(value, dict):
                logger.info(f"  Content: {json.dumps(value, indent=2)}")
                
                # Extract specific fields if they exist
                if 'message_id' in value:
                    logger.info(f"  Message ID: {value['message_id']}")
                if 'content' in value:
                    logger.info(f"  Message Content: {value['content']}")
            else:
                logger.info(f"  Content: {value}")
            
            logger.info("-" * 50)
            
        except Exception as e:
            logger.error(f"Error processing message: {e}")
            logger.info(f"Raw message: partition={message.partition}, offset={message.offset}, key={message.key}")
    
    def get_topic_metadata(self):
        """Get metadata about the topic"""
        if not self.consumer:
            logger.error("Consumer not initialized. Call connect() first.")
            return None
        
        try:
            partitions = self.consumer.partitions_for_topic(self.topic_name)
            
            logger.info(f"Topic '{self.topic_name}' metadata:")
            logger.info(f"  Partitions: {partitions}")
            
            return {
                'topic': self.topic_name,
                'partitions': list(partitions) if partitions else [],
                'consumer_group': self.group_id
            }
            
        except Exception as e:
            logger.error(f"Error getting topic metadata: {e}")
            return None
    
    def close(self):
        """Close the consumer connection"""
        if self.consumer:
            self.consumer.close()
            logger.info("Consumer connection closed")


def main():
    parser = argparse.ArgumentParser(description='Kafka Consumer Demo Script')
    parser.add_argument('--bootstrap-servers', default='10.10.100.93:9092',
                       help='Kafka bootstrap servers (default: localhost:9092)')
    parser.add_argument('--topic', default='test-topic',
                       help='Kafka topic name (default: test-topic)')
    parser.add_argument('--group-id', default='demo-consumer-group',
                       help='Consumer group ID (default: demo-consumer-group)')
    parser.add_argument('--max-messages', type=int,
                       help='Maximum number of messages to consume')
    parser.add_argument('--timeout', type=int,
                       help='Timeout in seconds for message consumption')
    parser.add_argument('--from-beginning', action='store_true',
                       help='Consume from the beginning of the topic')
    parser.add_argument('--metadata-only', action='store_true',
                       help='Only show topic metadata, do not consume messages')
    
    args = parser.parse_args()
    
    # Create consumer instance
    consumer_demo = KafkaConsumerDemo(args.bootstrap_servers, args.topic, args.group_id)
    
    # Determine offset reset strategy
    offset_reset = 'earliest' if args.from_beginning else 'latest'
    
    # Connect to Kafka
    if not consumer_demo.connect(auto_offset_reset=offset_reset):
        logger.error("Failed to connect to Kafka. Exiting.")
        sys.exit(1)
    
    try:
        # Show metadata if requested
        if args.metadata_only:
            consumer_demo.get_topic_metadata()
        else:
            # Start consuming messages
            if args.from_beginning:
                logger.info("Consuming messages from the beginning of the topic")
            else:
                logger.info("Consuming new messages (from latest offset)")
            
            success = consumer_demo.consume_messages(
                max_messages=args.max_messages,
                timeout_seconds=args.timeout
            )
            
            if not success:
                logger.error("Consumer demo failed!")
                sys.exit(1)
        
        logger.info("Consumer demo completed successfully!")
        
    except KeyboardInterrupt:
        logger.info("Consumer demo interrupted by user")
    except Exception as e:
        logger.error(f"Unexpected error: {e}")
        sys.exit(1)
    finally:
        consumer_demo.close()


if __name__ == "__main__":
    main()