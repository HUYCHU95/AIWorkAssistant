namespace AIWork.Application;
public record MailEnvelope(string From,string To,string Subject,string Body);
public interface IMailTransport{Task Send(MailEnvelope envelope,CancellationToken cancellationToken);}
