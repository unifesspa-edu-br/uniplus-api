namespace Unifesspa.UniPlus.Ingresso.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Microsoft.Extensions.Time.Testing;

using Unifesspa.UniPlus.Ingresso.Domain.Entities;
using Unifesspa.UniPlus.Ingresso.Domain.Enums;
using Unifesspa.UniPlus.Ingresso.Domain.Events;
using Unifesspa.UniPlus.Ingresso.Domain.ValueObjects;

public sealed class ConvocacaoTests
{
    private static readonly DateTimeOffset Instante = new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);

    private readonly Guid _chamadaId = Guid.CreateVersion7();
    private readonly Guid _inscricaoId = Guid.CreateVersion7();
    private readonly Guid _candidatoId = Guid.CreateVersion7();
    private readonly FakeTimeProvider _relogio = new(Instante);

    [Fact(DisplayName = "Convocação criada nasce pendente com os dados informados")]
    public void Criar_ComDadosValidos_NasceComStatusPendenteEOsDadosInformados()
    {
        ProtocoloConvocacao protocolo = ProtocoloConvocacao.Gerar(1, 1, _relogio);

        Convocacao convocacao = Convocacao.Criar(_chamadaId, _inscricaoId, _candidatoId, protocolo, 3, "CURSO01", _relogio);

        convocacao.Status.Should().Be(StatusConvocacao.Pendente);
        convocacao.ChamadaId.Should().Be(_chamadaId);
        convocacao.InscricaoId.Should().Be(_inscricaoId);
        convocacao.CandidatoId.Should().Be(_candidatoId);
        convocacao.Protocolo.Should().Be(protocolo);
        convocacao.Posicao.Should().Be(3);
        convocacao.CodigoCurso.Should().Be("CURSO01");
        convocacao.DataManifestacao.Should().BeNull();
    }

    [Fact(DisplayName = "Convocação criada registra uma única vez o evento de candidato convocado, no instante do relógio")]
    public void Criar_ComDadosValidos_RegistraUmUnicoEventoDeCandidatoConvocado()
    {
        ProtocoloConvocacao protocolo = ProtocoloConvocacao.Gerar(1, 1, _relogio);

        Convocacao convocacao = Convocacao.Criar(_chamadaId, _inscricaoId, _candidatoId, protocolo, 3, "CURSO01", _relogio);

        CandidatoConvocadoEvent evento = convocacao.DomainEvents.Should().ContainSingle().Which
            .Should().BeOfType<CandidatoConvocadoEvent>().Subject;
        evento.InscricaoId.Should().Be(_inscricaoId);
        evento.CandidatoId.Should().Be(_candidatoId);
        evento.ChamadaId.Should().Be(_chamadaId);
        evento.Protocolo.Should().Be(protocolo.Valor);
        evento.OccurredOn.Should().Be(Instante);
    }

    [Fact(DisplayName = "Com protocolo e relógio nulos, a criação falha pelo protocolo")]
    public void Criar_ProtocoloNuloERelogioNulo_FalhaPeloProtocolo()
    {
        Action criar = () => Convocacao.Criar(_chamadaId, _inscricaoId, _candidatoId, null!, 3, "CURSO01", null!);

        criar.Should().Throw<ArgumentNullException>().WithParameterName("protocolo");
    }
}
