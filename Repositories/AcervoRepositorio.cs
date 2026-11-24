using Microsoft.EntityFrameworkCore;
using UniColab.Api.Data;
using UniColab.Api.Entities;

namespace UniColab.Api.Repositories;

public interface IAcervoRepositorio
{
    Task<List<AreaConhecimento>> ListarAreasAsync(CancellationToken ct);
    Task<bool> AreaExisteAsync(short areaId, CancellationToken ct);
    IQueryable<Questao> Questoes(long instituicaoId);
    Task<Questao?> ObterQuestaoAsync(long instituicaoId, long id, CancellationToken ct);
    Task<List<Alternativa>> AlternativasAsync(long questaoId, CancellationToken ct);
    Task<Alternativa?> ObterAlternativaAsync(long questaoId, long alternativaId, CancellationToken ct);
    Task<List<Justificativa>> JustificativasAsync(long questaoId, CancellationToken ct);
    Task<Justificativa?> ObterJustificativaAsync(long alternativaId, CancellationToken ct);
    Task<string?> NomeAreaAsync(short areaId, CancellationToken ct);
    Task<List<long>> IdsComJustificativaCompletaAsync(long instituicaoId, IReadOnlyCollection<long>? filtro, CancellationToken ct);
    void Adicionar(Questao questao);
    void Adicionar(Alternativa alternativa);
    void Adicionar(Justificativa justificativa);
    Task ApagarDefinitivoAsync(long questaoId, long atorId, CancellationToken ct);
}

public sealed class AcervoRepositorio : IAcervoRepositorio
{
    private readonly UnicolabDbContext _db;

    public AcervoRepositorio(UnicolabDbContext db)
    {
        _db = db;
    }

    public Task<List<AreaConhecimento>> ListarAreasAsync(CancellationToken ct) =>
        _db.Areas.Where(a => a.Ativo).OrderBy(a => a.Nome).ToListAsync(ct);

    public Task<bool> AreaExisteAsync(short areaId, CancellationToken ct) =>
        _db.Areas.AnyAsync(a => a.Id == areaId && a.Ativo, ct);

    public IQueryable<Questao> Questoes(long instituicaoId) =>
        _db.Questoes.Where(q => q.InstituicaoId == instituicaoId);

    public Task<Questao?> ObterQuestaoAsync(long instituicaoId, long id, CancellationToken ct) =>
        _db.Questoes.FirstOrDefaultAsync(q => q.Id == id && q.InstituicaoId == instituicaoId, ct);

    public Task<List<Alternativa>> AlternativasAsync(long questaoId, CancellationToken ct) =>
        _db.Alternativas.Where(a => a.QuestaoId == questaoId).OrderBy(a => a.Ordem).ToListAsync(ct);

    public Task<Alternativa?> ObterAlternativaAsync(long questaoId, long alternativaId, CancellationToken ct) =>
        _db.Alternativas.FirstOrDefaultAsync(a => a.Id == alternativaId && a.QuestaoId == questaoId, ct);

    public Task<List<Justificativa>> JustificativasAsync(long questaoId, CancellationToken ct) =>
        _db.Justificativas
            .Where(j => _db.Alternativas.Any(a => a.Id == j.AlternativaId && a.QuestaoId == questaoId))
            .ToListAsync(ct);

    public Task<Justificativa?> ObterJustificativaAsync(long alternativaId, CancellationToken ct) =>
        _db.Justificativas.FirstOrDefaultAsync(j => j.AlternativaId == alternativaId, ct);

    public Task<string?> NomeAreaAsync(short areaId, CancellationToken ct) =>
        _db.Areas.Where(a => a.Id == areaId).Select(a => a.Nome).FirstOrDefaultAsync(ct);

    public Task<List<long>> IdsComJustificativaCompletaAsync(long instituicaoId, IReadOnlyCollection<long>? filtro, CancellationToken ct)
    {
        if (filtro is { Count: 0 }) return Task.FromResult(new List<long>());

        var consulta = _db.Questoes.Where(questao =>
            questao.InstituicaoId == instituicaoId
            && questao.Status == "publicada"
            && questao.Tipo == "multipla_escolha");
        if (filtro is not null)
        {
            consulta = consulta.Where(questao => filtro.Contains(questao.Id));
        }

        return consulta
            .Where(questao => _db.Alternativas.Count(alternativa => alternativa.QuestaoId == questao.Id) == 5)
            .Where(questao => _db.Alternativas.Count(alternativa =>
                alternativa.QuestaoId == questao.Id
                && _db.Justificativas.Any(justificativa =>
                    justificativa.AlternativaId == alternativa.Id
                    && justificativa.Explicacao.Length >= 10)) == 5)
            .Select(questao => questao.Id)
            .ToListAsync(ct);
    }

    public void Adicionar(Questao questao) => _db.Questoes.Add(questao);
    public void Adicionar(Alternativa alternativa) => _db.Alternativas.Add(alternativa);
    public void Adicionar(Justificativa justificativa) => _db.Justificativas.Add(justificativa);

    public Task ApagarDefinitivoAsync(long questaoId, long atorId, CancellationToken ct) =>
        _db.Database.ExecuteSqlInterpolatedAsync($"CALL sp_apagar_questao({questaoId}, {atorId})", ct);
}
