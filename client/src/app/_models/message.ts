export interface Message {
    id: number;
    senderId: number;
    senderUsername: string;
    senderPhotoURL: string;
    recipientId: number;
    recipientUsername: string;
    recipientPhotoURL: string;
    content: string;
    dateRead?: Date;
    messageSent: Date;
}